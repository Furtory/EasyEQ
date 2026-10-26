#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hGUI, $hDevice, $iChannel = 1
	Local $aiCCDests[2][2] = [ _
		[$CDEST_PITCH, 0x40], _
		[$CDEST_AMPLITUDE, 0x40]]

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(SelDevice(), $hGUI)

	_midi_SelectPatch($hDevice, $iChannel, $PGM_DBAR_ORGAN)
	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], 0x60)
	Next
	_midi_SendControlChange($hDevice, $iChannel, $CC_GP1, 127)

	MsgBox(0, "CC Destinations", "Press OK alter controller destinations (if supported)")
	For $i = 0 To 0x7F Step 8
		$aiCCDests[0][1] = $i ;$CDEST_PITCH property
		$aiCCDests[1][1] = $i ;$CDEST_AMPLITUDE property
		_midi_SetControllerDest($hDevice, $iChannel, $CC_GP1, $aiCCDests)
		Sleep(5)
	Next
	Sleep(1000)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)

	_midi_SendControlChange($hDevice, $iChannel, $CC_GP1, 0)
	_midi_SetControllerDest($hDevice, $iChannel, $CC_GP1)
	_midi_SelectPatch($hDevice, $iChannel, $PGM_GRAND_PIANO)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func SelDevice()
	Local $asDevices, $sDevList, $sDefaultDev
	Local $hCombo, $hOK, $iDevID

	$asDevices = _midi_EnumOutputs()
	For $i = 0 To UBound($asDevices) -1
		If Not $i Then $sDefaultDev = $asDevices[$i]
		$sDevList &= $asDevices[$i] & "|"
	Next

	GUICreate("Select Device", 270, 100)
	GUICtrlCreateLabel("Output", 14, 14, 56, 21, $SS_CENTERIMAGE)
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
