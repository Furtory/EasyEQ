#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hEdit
	Local $asDevices, $hDevice
	Local $aMsg, $sLine, $aRPN

	_midi_Startup()

	$hGUI = GUICreate("Close To Exit", 420, 500)
	GUICtrlCreateLabel("Close the window to finish", 4, 4, 200, 20)
	$hEdit = GUICtrlCreateEdit("", 4, 28, 412, 468)
	GUICtrlSetFont(-1, -1, -1, -1, "Consolas")

	$asDevices = _midi_EnumInputs()
	If Not @error Then $hDevice = _midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then

		GUISetState()
		GUICtrlSetData($hEdit, StringFormat("Change a RPN Value.\r\n\r\n"), 1)

		While 1
			$aMsg = _midi_ReadMsg($hDevice)
			If Not @error Then
				$aRPN = _midi_ReadRPN($hDevice, $aMsg)
				If Not @error Then
					$sLine = StringFormat("Patch: chan[%s] RPN[%d] P1/MSB[%02Xh] P2/LSB[%02Xh]\r\n", _
							$aMsg[1], $aRPN[0], $aRPN[1], $aRPN[2])
					GUICtrlSetData($hEdit, $sLine, 1)
				EndIf
			Else
				;GUIGetMsg introduces ~10ms delay in the loop to prevent CPU ramping.
				;To increase responsiveness it is recommended to idle the CPU only when the midi
				;input queue is empty (i.e. when _midi_ReadMsg fails).
				If GUIGetMsg() = $GUI_EVENT_CLOSE Then ExitLoop

				;GUIOnEvent mode can be used to decrease jitter caused by the above method.
			EndIf
		WEnd
	Else
		MsgBox(0, "Error", "An input device is unavailable.")
	EndIf

	_midi_CloseInput($hDevice)
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