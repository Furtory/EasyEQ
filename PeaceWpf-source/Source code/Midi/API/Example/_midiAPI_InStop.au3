#include <GUIConstants.au3>
#include <midiApi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice, $hStop

	$hGUI = GUICreate("Midi Input", 300, 220)
	GUICtrlCreateLabel("Close the window to finish", 90, 14, 200, 20, $SS_RIGHT)
	$hStop = GUICtrlCreateButton("Stop Input", 4, 4, 82, 40)
	GUICtrlCreateEdit("", 4, 48, 292, 168)
	GUIRegisterMsg($MM_MIM_OPEN, "MidiMsg")
	GUIRegisterMsg($MM_MIM_DATA, "MidiMsg")
	GUISetState()

	_midiAPI_Startup()

	If _midiAPI_InGetNumDevs() Then
		$hDevice = _midiAPI_InOpen(0, $hGUI, 0, $CALLBACK_WINDOW)

		While 1
			Switch GUIGetMsg()
				Case $GUI_EVENT_CLOSE
					ExitLoop
				Case $hStop
					_midiAPI_InStop($hDevice)
					GUICtrlSetData(-1, "Input stopped." & @CRLF, 1)
			EndSwitch
		WEnd

		_midiAPI_InStop($hDevice)
		_midiAPI_InClose($hDevice)
	Else
		MsgBox(0, "Error", "No devices were found")
	EndIf

	_midiAPI_Shutdown()
	GUIDelete($hGUI)
EndFunc   ;==>Example

Func MidiMsg($hWnd, $iMsg, $wParam, $lParam)
	#forceref $hWnd, $iMsg, $wParam, $lParam
	Switch $iMsg
		Case $MM_MIM_OPEN
			_midiAPI_InStart($wParam)
			GUICtrlSetData(-1, "Input started, Generate some messages." & @CRLF, 1)
		Case $MM_MIM_DATA
			;write midi messages to the edit box
			GUICtrlSetData(-1, BinaryMid($lParam, 1, 3) & @CRLF, 1)
	EndSwitch
EndFunc   ;==>MidiMsg

