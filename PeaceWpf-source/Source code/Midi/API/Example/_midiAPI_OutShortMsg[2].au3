#include <midiApi.au3>

Example()

Func Example()
	Local Const $CVM_NTEOFF = 0x80, $CVM_NTEON = 0x90, $CVM_BEND = 0xE0, $CVM_CC = 0xB0 ;channel voice messages
	Local Const $NTE_C4 = 0x3C, $NTE_EF4 = 0x3F, $NTE_F5 = 0x4D ;Notes
	Local Const $VEL_MF = 0x50 ;Velocity
	Local Const $BEND_UP_MAX = 0x3FFF, $MODWHEEL_MAX = 0x7F ;Limits
	Local Const $CC_MODWHEEL = 1 ;Control ID

	Local $hDevice
	Local $aMessages[10] = [ _
			BuildShortMsg($CVM_NTEON, 1, $NTE_C4, $VEL_MF), _
			BuildShortMsg($CVM_NTEON, 2, $NTE_EF4, $VEL_MF), _
			BuildShortMsg($CVM_BEND, 2, $BEND_UP_MAX), _
			BuildShortMsg($CVM_NTEON, 1, $NTE_F5, $VEL_MF), _
			BuildShortMsg($CVM_CC, 1, $CC_MODWHEEL, $MODWHEEL_MAX), _
			BuildShortMsg($CVM_NTEOFF, 1, $NTE_C4, 0), _
			BuildShortMsg($CVM_NTEOFF, 1, $NTE_F5, 0), _
			BuildShortMsg($CVM_NTEOFF, 2, $NTE_EF4, 0)]

	_midiAPI_Startup()
	$hDevice = _midiAPI_OutOpen(0)

	For $i = 0 To UBound($aMessages) - 1
		_midiAPI_OutShortMsg($hDevice, $aMessages[$i])
		Sleep(200)
	Next

	_midiAPI_OutClose($hDevice)
	_midiAPI_Shutdown()
EndFunc   ;==>Example

Func BuildShortMsg($iEvent, $iChannel, $iParam1, $iParam2 = 0)
	Local Const $CVM_BEND = 0xE0
	Local Static $tMsg = DllStructCreate("byte[3]")

	;Status Byte
	DllStructSetData($tMsg, 1, BitOR($iEvent, $iChannel - 1), 1)

	If $iEvent = $CVM_BEND Then
		;Pitch bend is a 14bit value over 2 bytes. (7bit/7bit split.)
		DllStructSetData($tMsg, 1, BitAND(0x7F, $iParam1), 2) ;LSB
		DllStructSetData($tMsg, 1, BitShift($iParam1, 7), 3) ;MSB
	Else
		DllStructSetData($tMsg, 1, $iParam1, 2) ;Param1
		DllStructSetData($tMsg, 1, $iParam2, 3) ;Param2
	EndIf

	Return Int(DllStructGetData($tMsg, 1))
EndFunc   ;==>BuildShortMsg
