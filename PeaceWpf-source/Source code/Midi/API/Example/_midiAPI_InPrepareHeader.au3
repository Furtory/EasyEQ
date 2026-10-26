#include <midiApi.au3>

Example()

Func Example()

	Local $hDevice, $iHdrFlag
	Local $pMidiHdr, $tMidiHdr, $tBuffer
	Local $iBuffSize = 128

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
			MsgBox(0, "Buffer", "Input buffer added")

			_midiAPI_InReset($hDevice) ;return buffer
			Do
				Sleep(10)
				$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
			Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

			;cleanup
			_midiAPI_InUnprepareHeader($hDevice, $pMidiHdr)

			;free the buffer and header
			$tBuffer = 0
			$tMidiHdr = 0

			MsgBox(0, "Buffer", "Input buffer freed")
		Else
			MsgBox(0, "Error", _midiAPI_InGetErrorText(@error))
		EndIf

		_midiAPI_InClose($hDevice)

	Else
		MsgBox(0, "Error", "Failed to open device.")
	EndIf

	_midiAPI_Shutdown()
EndFunc   ;==>Example
