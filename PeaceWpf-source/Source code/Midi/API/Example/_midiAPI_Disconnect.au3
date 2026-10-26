#include <GuiConstants.au3>
#include <midiApi.au3>

Example()

Func Example()

	Local $hMO_Dev, $hMI_Dev, $hGUI, $iDevID
	Local $hStatus, $hConnect, $hDisconnect

	MsgBox(0, "Midi Disconnect", "This demonstration requires a midi controller.")

	$hGUI = GUICreate("Midi Connect", 280, 80)
	$hStatus = GUICtrlCreateLabel('Press "Connect" to start.', 4, 4, 200, 40)
	$hConnect = GUICtrlCreateButton("Connect", 60, 35, 80, 30)
	$hDisconnect = GUICtrlCreateButton("Disconnect", 140, 35, 80, 30)

	_midiAPI_Startup()

	;Select and open devices.
	$iDevID = SelInputDevice()
	;there must be a callback attached to the input device.
	If Not @error Then $hMI_Dev = _midiAPI_InOpen($iDevID, $hGUI, 0, $CALLBACK_WINDOW)
	If Not @error Then $iDevID = SelOutputDevice()
	If Not @error Then $hMO_Dev = _midiAPI_OutOpen($iDevID)
	If Not @error Then

		_midiAPI_InStart($hMI_Dev)

		GUISetState()
		While WinExists($hGUI)
			Switch GUIGetMsg()
				Case $GUI_EVENT_CLOSE
					ExitLoop
				Case $hConnect
					_midiAPI_Connect($hMI_Dev, $hMO_Dev)
					If Not @error Then GUICtrlSetData($hStatus, "Connected, Play some notes!")
				Case $hDisconnect
					_midiAPI_Disconnect($hMI_Dev, $hMO_Dev)
					If Not @error Then GUICtrlSetData($hStatus, "Disconnected.")
			EndSwitch
		WEnd

		_midiAPI_InStop($hMI_Dev)

	Else
		MsgBox(0, "Error", "A device failed to open or doesn't exist.")
	EndIf

	If $hMO_Dev > 0 Then _midiAPI_OutClose($hMO_Dev)
	If $hMI_Dev > 0 Then _midiAPI_InClose($hMI_Dev)

	GUIDelete()

	_midiAPI_Shutdown()
EndFunc   ;==>Example

Func SelInputDevice()
	Local $tCaps, $sDevList, $sDevName

	;Enum Devices
	$tCaps = DllStructCreate($tag_midiincaps)
	For $i = 0 To _midiAPI_InGetNumDevs() -1
		_midiAPI_InGetDevCaps($i, DllStructGetPtr($tCaps))
		$sDevName = DllStructGetData($tCaps, "szPname")
		$sDevList &= $sDevName & "|"
	Next
	If Not $i Then Return SetError(1)

	;Launch selector gui
	Return SelDevice($sDevList, $sDevName, "Midi Input:")
EndFunc   ;==>SelInputDevice

Func SelOutputDevice()
	Local $tCaps, $sDevList, $sDevName

	;Enum Devices
	$tCaps = DllStructCreate($tag_midioutcaps)
	For $i = 0 To _midiAPI_OutGetNumDevs() -1
		_midiAPI_OutGetDevCaps($i, DllStructGetPtr($tCaps))
		If Not $i Then $sDevName = DllStructGetData($tCaps, "szPname")
		$sDevList &= DllStructGetData($tCaps, "szPname") & "|"
	Next
	If Not $i Then Return SetError(1)

	;Launch selector gui
	Return SelDevice($sDevList, $sDevName, "Midi Output:")
EndFunc   ;==>SelOutputDevice

Func SelDevice($sDevList, $sDefaultDev, $sPrompt)
	;Device selection GUI
	Local $hCombo, $hOK, $iDevID

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
EndFunc   ;==>SelDevice
