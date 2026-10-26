#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hTimer, $iTimeout = 500
	Local $asDevices, $hInDevice, $hOutDevice
	Local $adData, $sRetMsg, $dSysEx
	Local $sReport = "DevNum:\t[%s]\nAddress:\t[%s]\nData:\t[%s]\n"

	;Below holds a table of parameter locations and thier sizes.
	; - The address values must be exactly 3 bytes.
	;Address, Size
	Local Const $XG_ADDRESS_TABLE[5][2] = [ _
		[Binary("0x000000"), 7], _
		[Binary("0x000000"), 4], _
		[Binary("0x000004"), 1], _
		[Binary("0x000005"), 1], _
		[Binary("0x000006"), 1]] ;...etc

	;Indicies
	Local Enum $XG_SYSTEM_BLOCK, $XG_MSTR_TUNE, $XG_MSTR_VOL, $XG_MSTR_ATTEN, $XG_TPOSE ;...etc

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$asDevices = _midi_EnumInputs()
	If Not @error Then $hInDevice = _Midi_OpenInput(SelDevice($asDevices, "Input:"), $hGUI)
	If Not @error Then $asDevices = _midi_EnumOutputs()
	If Not @error Then $hOutDevice = _Midi_OpenOutput(SelDevice($asDevices, "Output:"), $hGUI)
	If Not @error Then

		 _midi_SendXGDumpRequest($hOutDevice, _
			$XG_ADDRESS_TABLE[$XG_SYSTEM_BLOCK][0], _
			$XG_ADDRESS_TABLE[$XG_SYSTEM_BLOCK][1])

		$hTimer = TimerInit()
		Do
			If _midi_ReadSysEx($hInDevice, $dSysEx) Then
				$adData = _midi_ReadXGDataDump($dSysEx)
				If Not @error Then ExitLoop
			EndIf
		Until TimerDiff($hTimer) > $iTimeout

		If Not BinaryLen($dSysEx) Then
			$sRetMsg = "No response was recieved."
		Else
			$sRetMsg = StringFormat($sReport,  _
					$adData[0], _  ; Device number (0 - based)
					$adData[1], _  ; Address
					$adData[2])    ; Data
		EndIf

		MsgBox(0, "DT1",  $sRetMsg)

	Else
		MsgBox(0, "Error", "A input or output device is unavailable.")
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
		$sDevList &= $asDevices[$i] & "|"
	Next
	$sDefaultDev = $asDevices[$i-1]

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
