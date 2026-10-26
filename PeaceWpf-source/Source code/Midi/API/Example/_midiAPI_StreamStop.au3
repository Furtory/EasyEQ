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

		;prepare and queue the buffer
		_midiAPI_OutPrepareHeader($hStream, $pMidiHdr)
		_midiAPI_StreamOut($hStream, $pMidiHdr)
		_midiAPI_StreamRestart($hStream)

		;Stop the stream midway.
		Sleep(800)
		_midiAPI_StreamStop($hStream)
		Do
			Sleep(10)
			$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
		Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

		;Resets pitch bend, sustain etc..
		_midiAPI_OutReset($hStream)

		MsgBox(0, "Midi", "Stream stopped, Press OK to restart playback")

		;Restart the stream.

		;clear the "done" flag
		$iHdrFlag = BitAND($iHdrFlag, BitNOT($MHDR_DONE))
		DllStructSetData($tMidiHdr, "dwFlags", $iHdrFlag)

		;requeue buffer
		_midiAPI_StreamOut($hStream, $pMidiHdr)

		;restart
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
