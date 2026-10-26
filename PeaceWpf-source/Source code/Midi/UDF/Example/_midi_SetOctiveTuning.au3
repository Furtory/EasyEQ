
#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $iChannel = 1
	Local $aiTunings[12] = [ _
			0x00, 0x40, 0x63, 0x50, _
			0x7F, 0x2C, 0x1D, 0x33, _
			0x52, 0x15, 0x6A, 0x20]
	Local $iChannnels = BitOR($CH_MSK_1, $CH_MSK_3)

	$hGUI = GUICreate("dummy")
	_midi_Startup()

	$hDevice = _midi_OpenOutput(SelDevice(), $hGUI)
	PlayNotes($hDevice, $iChannel)

	MsgBox(0, "Octive/Scale Tuning", "Press OK retune the instrument on channels 1 & 3. (if supported)")
	_midi_SetOctiveTuning($hDevice, $aiTunings, $iChannnels)

	PlayNotes($hDevice, $iChannel)

	_midi_SetOctiveTuning($hDevice)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func PlayNotes($hDevice, $iChannel)
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $iVelocity = 0x60

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], $iVelocity)
		Sleep(200)
	Next

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)
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