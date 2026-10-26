#include <midiApi.au3>

Example()

Func Example()

	Local $hDevice, $iHdrFlag
	Local $pMidiHdr, $tMidiHdr, $tBuffer

	;A 3 byte message used for demonstration only.
	;_midiAPI_OutShortMsg should be used for this.
	Local $dMidiMsg = Binary("0x903C50")
	Local $iBuffSize = BinaryLen($dMidiMsg)

	_midiAPI_Startup()

	;open devices.
	$hDevice = _midiAPI_OutOpen(0)
	If Not @error Then

		;Create a buffer and fill it
		$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iBuffSize))
		DllStructSetData($tBuffer, 1, $dMidiMsg)

		;Create the header
		$tMidiHdr = DllStructCreate($tag_midihdr)
		$pMidiHdr = DllStructGetPtr($tMidiHdr)
		DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
		DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))

		;Send data
		_midiAPI_OutPrepareHeader($hDevice, $pMidiHdr)
		_midiAPI_OutLongMsg($hDevice, $pMidiHdr)
		If Not @error Then
			MsgBox(0, "Buffer", "Output buffer sent")

			;wait for buffer to return.
			Do
				Sleep(10)
				$iHdrFlag = DllStructGetData($tMidiHdr, "dwFlags")
			Until BitAND($iHdrFlag, $MHDR_DONE) = $MHDR_DONE

			;cleanup
			_midiAPI_OutUnprepareHeader($hDevice, $pMidiHdr)

			;free the buffer and header
			$tBuffer = 0
			$tMidiHdr = 0

			MsgBox(0, "Buffer", "Output buffer freed")
		Else
			MsgBox(0, "Error", _midiAPI_OutGetErrorText(@error))
		EndIf

		_midiAPI_OutClose($hDevice)

	Else
		MsgBox(0, "Error", "Failed to open device.")
	EndIf

	_midiAPI_Shutdown()
EndFunc   ;==>Example
