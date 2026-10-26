#include <midiApi.au3>

Example()

Func Example()

	Local $hStream, $pMidiHdr, $tBuffer, $tMidiHdr, $iHdrFlag
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
		;Prepare & send buffer
		_midiAPI_OutPrepareHeader($hStream, $pMidiHdr)
		_midiAPI_StreamOut($hStream, $pMidiHdr)

		;Start playback
		_midiAPI_StreamRestart($hStream)
		Sleep(800)

		;Pause
		_midiAPI_StreamPause($hStream)
		MsgBox(0, "Midi", "Stream paused, Press OK to continue playback")

		_midiAPI_StreamRestart($hStream)
		Do
			Sleep(100)
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
