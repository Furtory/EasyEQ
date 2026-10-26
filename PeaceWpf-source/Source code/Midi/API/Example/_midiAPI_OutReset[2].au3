#include <midiApi.au3>

Example()

Func Example()

	Local $hDevice, $iHdrFlag
	Local $pMidiHdr, $tMidiHdr, $tBuffer
	Local $dStream = Binary("0x0000000000000000903C5000" & _
			"1800000000000000913F50000C00000000000000E17F7F00" & _
			"0C00000000000000904D50001800000000000000B07B0000" & _
			"0C00000000000000813F0000")
	Local $iBuffSize = BinaryLen($dStream)

	_midiAPI_Startup()

	;open devices.
	$hDevice = _midiAPI_StreamOpen(0)
	If Not @error Then

		;Create a buffer and fill it
		$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBuffSize))
		DllStructSetData($tBuffer, 1, $dStream)

		;Create the header
		$tMidiHdr = DllStructCreate($tag_midihdr)
		$pMidiHdr = DllStructGetPtr($tMidiHdr)
		DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
		DllStructSetData($tMidiHdr, "dwBufferLength", $iBuffSize)
		DllStructSetData($tMidiHdr, "dwBytesRecorded", BinaryLen($dStream))

		;Send data
		_midiAPI_OutPrepareHeader($hDevice, $pMidiHdr)
		_midiAPI_StreamOut($hDevice, $pMidiHdr)
		If Not @error Then
			MsgBox(0, "Buffer", "Output buffer sent")

			_midiAPI_StreamRestart($hDevice)
			Sleep(800)

			;interrupt & return buffer.
			_midiAPI_OutReset($hDevice)
			MsgBox(0, "Interrupt stream", "Output has been reset - buffer will return.")
			Do
				Sleep(100)
				$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
			Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

			;cleanup
			_midiAPI_OutUnprepareHeader($hDevice, $pMidiHdr)

			;free the buffer and header
			$tBuffer = 0
			$tMidiHdr = 0

			MsgBox(0, "Buffer", "Buffer freed.")
		Else
			MsgBox(0, "Error", _midiAPI_OutGetErrorText(@error))
		EndIf

		_midiAPI_OutClose($hDevice)

	Else
		MsgBox(0, "Error", "Failed to open device.")
	EndIf

	_midiAPI_Shutdown()
EndFunc   ;==>Example
