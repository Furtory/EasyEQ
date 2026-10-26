#include <midiApi.au3>

Example()

Func Example()
	_midiAPI_Startup()

	_midiAPI_InOpen(-30)
	If @error Then MsgBox(0, "error", _midiAPI_InGetErrorText(@error))

	_midiAPI_Shutdown()
EndFunc   ;==>Example
