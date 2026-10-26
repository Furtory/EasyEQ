#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hEdit
	Local $asDevices, $hDevice
	Local $aMsg, $sLine

	_midi_Startup()

	$hGUI = GUICreate("Close To Exit", 220, 500)
	GUICtrlCreateLabel("Close the window to finish", 4, 4, 200, 20)
	$hEdit = GUICtrlCreateEdit("", 4, 28, 212, 468)
	GUICtrlSetFont(-1, -1, -1, -1, "Consolas")

	$asDevices = _midi_EnumInputs()
	If Not @error Then $hDevice = _midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then

		GUISetState()
		GUICtrlSetData($hEdit, StringFormat("Move the modulation wheel.\r\n\r\n"), 1)

		While 1
			$aMsg = _midi_ReadMsg($hDevice)
			If Not @error Then
				Switch $aMsg[0]
					Case $MSG_CC
						If $aMsg[2] = $CC_MODWHEEL Then
							$sLine = StringFormat("ModWheel chan[%s] value[%02X]\r\n", $aMsg[1], $aMsg[3])
							GUICtrlSetData($hEdit, $sLine, 1)
						EndIf
				EndSwitch
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