#include <midiApi.au3>

Example()

Func Example()
	_midiAPI_Startup()

	_midiAPI_OutOpen(-30)
	If @error Then MsgBox(0, "error", _midiAPI_OutGetErrorText(@error))

	_midiAPI_Shutdown()
EndFunc   ;==>Example
