#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hEdit
	Local $asDevices, $hInDevice, $hOutDevice
	Local $aMsg, $sLine

	_midi_Startup()

	$hGUI = GUICreate("Close To Exit", 220, 500)
	GUICtrlCreateLabel("Close the window to finish", 4, 4, 292, 20)
	$hEdit = GUICtrlCreateEdit("", 4, 28, 212, 468)
	GUICtrlSetFont(-1, -1, -1, -1, "Consolas")

	$asDevices = _midi_EnumInputs()
	If Not @error Then $hInDevice = _Midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then $asDevices = _midi_EnumOutputs()
	If Not @error Then $hOutDevice = _Midi_OpenOutput(SelDevice($asDevices, "Output:"), $hGUI)
	If Not @error Then

		GUISetState()
		GUICtrlSetData($hEdit, StringFormat("Generate some messages.\r\n\r\n"), 1)
		GUICtrlSetData($hEdit, StringFormat("[MSG]  [CH] [PAR1] [PAR2]\r\n\r\n"), 1)

		While 1
			$aMsg = _midi_ReadMsg($hInDevice)
			If Not @error Then

				$sLine = StringFormat("[0x%X] [%d]  [0x%02X] [0x%02X]\r\n", $aMsg[0], $aMsg[1], $aMsg[2], $aMsg[3])
				GUICtrlSetData($hEdit, $sLine, 1)

				_midi_SendMsg($hOutDevice, $aMsg)
			Else
				;GUIGetMsg introduces ~10ms delay in the loop to prevent CPU ramping.
				;To increase responsiveness it is recommended to idle the CPU only when the midi
				;input queue is empty (i.e. when _midi_ReadMsg fails).
				If GUIGetMsg() = $GUI_EVENT_CLOSE Then ExitLoop

				;GUIOnEvent mode can be used to decrease jitter caused by the above method.
			EndIf
		WEnd

	Else
		MsgBox(0, "Error", "An input or output device is unavailable.")
	EndIf

	_Midi_CloseInput($hInDevice)
	_Midi_CloseOutput($hOutDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func SelDevice($asDevices, $sPrompt)
	Local $sDevList, $sDefaultDev
	Local $hCombo, $hOK, $iDevID

	For $i = 0 To UBound($asDevices) -1
		If Not $i Then $sDefaultDev = $asDevices[$i]
		$sDevList &= $asDevices[$i] & "|"
	Next

	GUICreate("Select Device", 270, 100)
	GUICtrlCreateLabel($sPrompt, 14, 14, 56, 21, $SS_CENTERIMAGE)
	$hCombo = GUICtrlCreateCombo("", 74, 14, 182, 25, $CBS_DROPDOWNLIST)
	GUICtrlSetData($hCombo, $sDevList, $sDefaultDev)
	$hOK = GUICtrlCreateButton("Select", 176, 61, 80, 25, $BS_DEFPUSHBUTTON)
	GUISetState()

	While 1
		Switch GUIGetMsg()
			Case $hOK, $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd
	$iDevID = GUICtrlSendMsg($hCombo, $CB_GETCURSEL, 0, 0)
	GUIDelete()

	Return $iDevID
EndFunc
