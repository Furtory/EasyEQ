#include <midiApi.au3>

Example()

Func Example()

	Local $hStream, $pMidiHdr, $tBuffer, $tMidiHdr, $tStreamPos, $iHdrFlag
	Local $iStreamPos, $sUnit
	Local $dStream = Binary("0x0000000000000000903C5000" & _
			"1800000000000000913F50000C00000000000000E17F7F00" & _
			"0C00000000000000904D50001800000000000000803C0000" & _
			"0000000000000000804D00000C00000000000000813F0000")

	;Create and fill the buffer
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", BinaryLen($dStream)))
	DllStructSetData($tBuffer, 1, $dStream)

	;Create the header
	$tMidiHdr = DllStructCreate($tag_midihdr)
	$pMidiHdr = DllStructGetPtr($tMidiHdr)
	DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
	DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))
	DllStructSetData($tMidiHdr, "dwBytesRecorded", BinaryLen($dStream))

	_midiAPI_Startup()

	$hStream = _midiAPI_StreamOpen(0)
	If Not @error Then

		;prepare and queue the buffer
		_midiAPI_OutPrepareHeader($hStream, $pMidiHdr)
		_midiAPI_StreamOut($hStream, $pMidiHdr)

		;Start the stream
		_midiAPI_StreamRestart($hStream)


		;pause the stream midway
		Sleep(800)
		_midiAPI_StreamPause($hStream)

		;create the struct, request that the position be returned as ticks.
		$tStreamPos = DllStructCreate($tag_mmtime)
		DllStructSetData($tStreamPos, "wType", $TIME_TICKS)

		;get the position. (the function may not return the type that was requested)
		_midiAPI_StreamPosition($hStream, DllStructGetPtr($tStreamPos), DllStructGetSize($tStreamPos))
		$iStreamPos = DllStructGetData($tStreamPos, "dwData")
		$sUnit = GetUnit(DllStructGetData($tStreamPos, "wType"))

		MsgBox(0, "Midi", StringFormat( _
				"The stream paused at %d %s.\r\nPress OK to continue playback", _
				$iStreamPos, $sUnit))

		;Continue the stream
		_midiAPI_StreamRestart($hStream)
		Do
			Sleep(10)
			$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
		Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

		;cleanup
		_midiAPI_OutUnprepareHeader($hStream, $pMidiHdr)
		_midiAPI_StreamClose($hStream)
	Else
		MsgBox(0, "Error", "Could not open output device.")
	EndIf
	_midiAPI_Shutdown()
EndFunc   ;==>Example

Func GetUnit($iType)
	Switch $iType
		Case $TIME_MS
			Return "milliseconds"
		Case $TIME_SAMPLES
			Return "samples"
		Case $TIME_BYTES
			Return "bytes"
		Case $TIME_MIDI
			Return "(songptrpos)"
		Case $TIME_TICKS
			Return "ticks"
	EndSwitch
EndFunc   ;==>GetUnit
