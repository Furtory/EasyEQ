#include <midiApi.au3>

Example()

Func Example()
	Local $hCallBack, $hDevice, $iInstance = 100


	$hCallBack = DllCallbackRegister("MidiCallback", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")

	_midiAPI_Startup()
	If _midiAPI_InGetNumDevs() Then
		;Open the first device with a callback function mechanism
		$hDevice = _midiAPI_InOpen(0, DllCallbackGetPtr($hCallBack), $iInstance, $CALLBACK_FUNCTION)
		If @error Then MsgBox(0, "Error", "Device failed to open")

		_midiAPI_InClose($hDevice)
	Else
		MsgBox(0, "Error", "No devices were found")
	EndIf

	_midiAPI_Shutdown()
	DllCallbackFree($hCallBack)
EndFunc   ;==>Example

Func MidiCallback($hDev, $iMsg, $iInst, $iParam1, $lParam2)
	#forceref $hDev, $iMsg, $iInst, $iParam1, $lParam2

	Switch $iMsg
		Case $MIM_OPEN
			MsgBox(0, "Opened device:", StringFormat("Handle:[%s] Instance:[%s]", $hDev, $iInst))

		Case $MIM_CLOSE
			MsgBox(0, "Closed device:", StringFormat("Handle:[%s] Instance:[%s]", $hDev, $iInst))
	EndSwitch
EndFunc   ;==>MidiCallback
