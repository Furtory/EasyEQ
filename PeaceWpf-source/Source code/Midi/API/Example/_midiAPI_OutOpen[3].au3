#include <midiApi.au3>

Example()

Func Example()
	Local $hCallBack, $hDevice, $iInstance = 100

	$hCallBack = DllCallbackRegister("MidiCallback", "none", "hwnd;uint;dword_ptr;dword_ptr;dword_ptr")

	_midiAPI_Startup()

	;Open device with a function callback mechanism
	$hDevice = _midiAPI_OutOpen($MIDI_MAPPER, DllCallbackGetPtr($hCallBack), $iInstance, $CALLBACK_FUNCTION)

	_midiAPI_OutClose($hDevice)

	_midiAPI_Shutdown()
EndFunc   ;==>Example

Func MidiCallback($hDevice, $iMsg, $iInst, $iParam1, $lParam2)
	#forceref $hDevice, $iMsg, $iInst, $iParam1, $lParam2
	Switch $iMsg
		Case $MOM_OPEN
			MsgBox(0, "Opened device:", StringFormat("Handle:[%s] Instance:[%s]", $hDevice, $iInst))

		Case $MOM_CLOSE
			MsgBox(0, "Closed device:", StringFormat("Handle:[%s] Instance:[%s]", $hDevice, $iInst))
	EndSwitch
EndFunc   ;==>MidiCallback
