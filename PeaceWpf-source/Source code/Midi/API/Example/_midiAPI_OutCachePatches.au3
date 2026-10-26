#include <GUIConstants.au3>
#include <midiApi.au3>

Example()

Func Example()
	Local $tPatchArray, $pPatchArray
	Local $hDevice, $iBank, $iCacheOption
	Local Const $iDefaultBank = 0
	Local $iChannels, $bUsed, $sReport
	Local $iQueryPatch = 2

	MsgBox(0, "Requirement:", _
			"This demonstration requires an internal synthesiser that supports patch caching.")

	_midiAPI_Startup()
	$hDevice = _midiAPI_OutOpen(SelOutputDevice())

	If Not @error Then

		$tPatchArray = DllStructCreate($tag_patcharray)
		$pPatchArray = DllStructGetPtr($tPatchArray)
		$iBank = $iDefaultBank
		$iCacheOption = $MIDI_CACHE_QUERY

		_midiAPI_OutCachePatches($hDevice, $iBank, $pPatchArray, $iCacheOption)
		If Not @error Then
			;Get the channels that use patch 2
			$iChannels = DllStructGetData($tPatchArray, "KEYARRAY", $iQueryPatch)

			$sReport = StringFormat("@Patch %d:\n\n", $iQueryPatch)
			For $i = 0 To 15
				$bUsed = (BitAND(0x01, BitShift($iChannels, $i)) = 1)
				$sReport &= StringFormat("Ch :%d\tUsed: %s\n", $i + 1, $bUsed)
			Next

			MsgBox(0, "Result", $sReport)
		Else
			MsgBox(0, "Error", _midiAPI_OutGetErrorText(@error))
		EndIf

		_midiAPI_OutClose($hDevice)
	Else
		MsgBox(0, "Error", "Could not open the device.")
	EndIf

	_midiAPI_Shutdown()

EndFunc   ;==>Example

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
