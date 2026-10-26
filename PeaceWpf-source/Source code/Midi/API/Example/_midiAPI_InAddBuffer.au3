#include <midiApi.au3>

Example()

Func Example()

	Local $hDevice, $iHdrFlag
	Local $pMidiHdr, $tMidiHdr, $tBuffer
	Local $iBuffSize = 128
	Local $dData, $iBytesRead

	MsgBox(0, "Requirement", "This demonstration requires a midi input device.")

	_midiAPI_Startup()

	;open devices.
	$hDevice = _midiAPI_InOpen(0)
	If Not @error Then

		;Create a buffer
		$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBuffSize))

		;Create the header
		$tMidiHdr = DllStructCreate($tag_midihdr)
		$pMidiHdr = DllStructGetPtr($tMidiHdr)
		DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
		DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))

		_midiAPI_InPrepareHeader($hDevice, $pMidiHdr)
		_midiAPI_InAddBuffer($hDevice, $pMidiHdr)
		If Not @error Then
			MsgBox(0, "Midi", "A buffer has been sent to the input device.")

			;manually return buffer.
			_midiAPI_InReset($hDevice)
			Do
				Sleep(10)
				$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
			Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

			;The buffer would have returned automatically if a long msg was recieved.
			;If that happened we could read the output.
			$iBytesRead = DllStructGetData($tMidiHdr, "dwBytesRecorded")
			$dData = DllStructGetData($tBuffer, 1)
			$dData = BinaryMid($dData, 1, $iBytesRead)
			MsgBox(0, "Midi", StringFormat("Buffer contents: %s\nWe manually recalled the buffer, so it will be empty.", $dData))

			;cleanup
			_midiAPI_InUnprepareHeader($hDevice, $pMidiHdr)

			;free the buffer and header
			$tBuffer = 0
			$tMidiHdr = 0

			MsgBox(0, "Midi", "The buffer has been freed.")
		Else
			MsgBox(0, "Error", _midiAPI_InGetErrorText(@error))
		EndIf

		_midiAPI_InClose($hDevice)

	Else
		MsgBox(0, "Error", "Failed to open device.")
	EndIf

	_midiAPI_Shutdown()
EndFunc   ;==>Example
