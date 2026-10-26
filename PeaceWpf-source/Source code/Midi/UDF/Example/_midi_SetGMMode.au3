#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice, $iChannel = 1
	Local $iBankCourse = 121, $iBankFine = 2, $iProgram = 128

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$hDevice = _Midi_OpenOutput(SelDevice(), $hGUI)
	_midi_SetGMMode($hDevice, $GM_MODE_GM2)

	MsgBox(0, "Select Patch", "If the device supports the GM2 sound set, you should hear a laser." & @CRLF & _
			"If not, you will probably hear a gunshot.")
	_midi_SelectPatch($hDevice, $iChannel, $iProgram, $iBankCourse, $iBankFine)

	_midi_SendNoteOn($hDevice, $iChannel, 0x3C, 0x60)
	Sleep(1000)
	_midi_SendNoteOff($hDevice, $iChannel, 0x3C)
	Sleep(500)

	_midi_SetGMMode($hDevice, $GM_MODE_OFF)
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
