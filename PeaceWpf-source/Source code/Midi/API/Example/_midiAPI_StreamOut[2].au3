#include <GUIConstants.au3>
#include <midiApi.au3>

Global $hGUI
Global $hBtnStart, $hBtnPause, $hBtnStop, $hSldTempo

Global Enum $KICK, $SNARE, $HIHAT
Global $ahPerc[6][16]

;Notes on and off.
Global $adNotes[3][2] = [ _
		[Binary("0x992450"), Binary("0x992400")], _
		[Binary("0x992850"), Binary("0x992800")], _
		[Binary("0x992A50"), Binary("0x992A00")]]

Main()

Func Main()
	Local $hStream, $bStopped = True

	GenerateGUI()
	GUIRegisterMsg($MM_MOM_POSITIONCB, "midiCallback")
	GUIRegisterMsg($MM_MOM_DONE, "midiCallback")

	_midiAPI_Startup()
	$hStream = _midiAPI_StreamOpen(0, $hGUI, 0, $CALLBACK_WINDOW)
	If @error Then
		MsgBox(0, "Error", "Cannot open output device.")
		GUIDelete($hGUI)
		_midiAPI_Shutdown()
		Return
	EndIf

	;4 ticks per crotchet (1 tick = 1 semiquaver)
	SetTiming($hStream, 4, GUICtrlRead($hSldTempo))

	While WinExists($hGUI)
		Switch GUIGetMsg()
			Case $GUI_EVENT_CLOSE
				ExitLoop

			Case $hBtnStart
				If $bStopped Then
					;queue the first buffer
					Loop($hStream, False, True)
					$bStopped = False
				EndIf

				;start the stream
				_midiAPI_StreamRestart($hStream)

			Case $hBtnStop

				;returns all buffers
				_midiAPI_StreamStop($hStream)
				$bStopped = True

			Case $hBtnPause
				_midiAPI_StreamPause($hStream)

			Case $hSldTempo
				SetTiming($hStream, 4, GUICtrlRead($hSldTempo))

		EndSwitch
	WEnd

	;Cleanup
	Loop($hStream, True)
	GUIDelete($hGUI)
	_midiAPI_StreamClose($hStream)
	_midiAPI_Shutdown()
EndFunc   ;==>Main

Func GenerateGUI()
	;Create the GUI
	$hGUI = GUICreate("Drum Machine", 800, 450)

	$hBtnStart = GUICtrlCreateButton("Start", 50, 14, 70, 40)
	$hBtnPause = GUICtrlCreateButton("Pause", 120, 14, 70, 40)
	$hBtnStop = GUICtrlCreateButton("Stop", 190, 14, 70, 40)
	$hSldTempo = GUICtrlCreateSlider(300, 20, 200, 25)
	GUICtrlSetLimit(-1, 210, 80)
	GUICtrlSetData(-1, 120)
	GUICtrlCreateLabel("80", 280, 20, 20, 40, BitOR($SS_CENTERIMAGE, $SS_RIGHT))
	GUICtrlCreateLabel("210", 500, 20, 20, 40, $SS_CENTERIMAGE)

	GUICtrlCreateLabel("HiHat", 14, 95, 30, 40, $SS_CENTERIMAGE)
	GUICtrlCreateLabel("Snare", 14, 140, 30, 40, $SS_CENTERIMAGE)
	GUICtrlCreateLabel("Kick", 14, 185, 30, 40, $SS_CENTERIMAGE)
	GUICtrlCreateLabel("HiHat", 14, 280, 30, 40, $SS_CENTERIMAGE)
	GUICtrlCreateLabel("Snare", 14, 325, 30, 40, $SS_CENTERIMAGE)
	GUICtrlCreateLabel("Kick", 14, 370, 30, 40, $SS_CENTERIMAGE)

	For $i = 0 To 15
		If Not Mod($i, 4) Then
			GUICtrlCreateLabel(Ceiling($i / 4) + 1, $i * 45 + 50, 70, 40, 20, BitOR($SS_CENTER, $SS_CENTERIMAGE))
		EndIf

		$ahPerc[$HIHAT][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 95, 40, 40, $BS_PUSHLIKE)
		If Not Mod($i, 2) Then GUICtrlSetState(-1, $GUI_CHECKED)
		$ahPerc[$SNARE][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 140, 40, 40, $BS_PUSHLIKE)
		If Mod($i, 8) = 4 Then GUICtrlSetState(-1, $GUI_CHECKED)
		$ahPerc[$KICK][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 185, 40, 40, $BS_PUSHLIKE)
		If Not Mod($i, 8) Then GUICtrlSetState(-1, $GUI_CHECKED)

		If Not Mod($i, 4) Then
			GUICtrlCreateLabel(Ceiling(($i + 16) / 4) + 1, $i * 45 + 50, 255, 40, 20, BitOR($SS_CENTER, $SS_CENTERIMAGE))
		EndIf

		$ahPerc[3 + $HIHAT][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 280, 40, 40, $BS_PUSHLIKE)
		If Not Mod($i, 2) Then GUICtrlSetState(-1, $GUI_CHECKED)
		$ahPerc[3 + $SNARE][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 325, 40, 40, $BS_PUSHLIKE)
		If Mod($i, 8) = 4 Then GUICtrlSetState(-1, $GUI_CHECKED)
		$ahPerc[3 + $KICK][$i] = GUICtrlCreateCheckbox("", $i * 45 + 50, 370, 40, 40, $BS_PUSHLIKE)
		If Not Mod($i, 8) Then GUICtrlSetState(-1, $GUI_CHECKED)

	Next

	GUISetState()
EndFunc   ;==>GenerateGUI

Func Loop($hStream, $bDestroy = False, $bRestart = False)
	Local $pMidiHdr, $tMidiHdr, $tBuffer
	Local $pBuffer, $pEvent

	;Store 2 buffers, we can get one ready while the other is playing.
	;$aBuffers[$index][$pMidiHdr, $tMidiHdr, $tBuffer]
	Local Static $aBuffers[2][3], $iBuffIdx = 1
	Local Static $aPercOn[3], $iDelta = 0

	;Cleanup on exit.
	If $bDestroy Then
		For $iBuffIdx = 0 To 1
			_midiAPI_OutUnPrepareHeader($hStream, $aBuffers[$iBuffIdx][0])
			$aBuffers[$iBuffIdx][0] = 0
			$aBuffers[$iBuffIdx][1] = 0
			$aBuffers[$iBuffIdx][2] = 0
		Next
		Return
	EndIf

	;Change which buffer we are working on.
	$iBuffIdx = BitXOR($iBuffIdx, 1)
	;(make sure we start from the top row when we hit play)
	If $bRestart Then $iBuffIdx = 0
	$pMidiHdr = $aBuffers[$iBuffIdx][0]
	$tMidiHdr = $aBuffers[$iBuffIdx][1]
	$tBuffer = $aBuffers[$iBuffIdx][2]

	;Setup a buffer and header if one doesn't exist
	If Not $pMidiHdr Then
		CreateBuffer($pMidiHdr, $tMidiHdr, $tBuffer, 1280)
		$pBuffer = DllStructGetPtr($tBuffer)
		$aBuffers[$iBuffIdx][0] = $pMidiHdr
		$aBuffers[$iBuffIdx][1] = $tMidiHdr
		$aBuffers[$iBuffIdx][2] = $tBuffer
		_midiAPI_OutPrepareHeader($hStream, $pMidiHdr)
	EndIf
	$pBuffer = DllStructGetPtr($tBuffer)
	$pEvent = $pBuffer

	;For each column of buttons in set
	For $i = 0 To 15

		;Create a note on event if need be.
		For $j = 0 To 2
			If BitAND(GUICtrlRead($ahPerc[$iBuffIdx * 3 + $j][$i]), $GUI_CHECKED) Then
				$aPercOn[$j] = True
				CreateEvent($pEvent, $adNotes[$j][0], $iDelta)
				$iDelta = 0
			EndIf
		Next

		;move to next semiquaver
		$iDelta += 1

		;Turn the note off if need be.
		For $j = 0 To 2
			If $aPercOn[$j] Then
				$aPercOn[$j] = False
				CreateEvent($pEvent, $adNotes[$j][1], $iDelta)
				$iDelta = 0
			EndIf
		Next

		;Trigger a callback on beat 4.
		;(determines when we fill & send the next buffer.)
		If $i = 12 Then
			CreateEvent($pEvent, 0, $iDelta)
			$iDelta = 0
		EndIf
	Next

	;Set the cumulative size of all events in the buffer.
	DllStructSetData($tMidiHdr, "dwBytesRecorded", $pEvent - $pBuffer)

	;Queue the buffer.
	_midiAPI_StreamOut($hStream, $pMidiHdr)
EndFunc   ;==>Loop

Func CreateEvent(ByRef $pEvent, $dData, $iDelta)
	Local $tEvent, $iEvent

	;Create the "beacon" (callback + no operation) event.
	If $dData = 0 Then
		$iEvent = BitOR($MEVT_F_CALLBACK, BitShift($MEVT_NOP, -24))
	Else
		$iEvent = BitOR($MEVT_F_SHORT, $dData)
	EndIf

	;Create a short midi event.
	$tEvent = DllStructCreate($tag_midievent, $pEvent)
	DllStructSetData($tEvent, "dwDeltaTime", $iDelta)
	DllStructSetData($tEvent, "dwEvent", $iEvent)

	$pEvent += DllStructGetSize($tEvent)
EndFunc   ;==>CreateEvent

Func CreateBuffer(ByRef $pMidiHdr, ByRef $tMidiHdr, ByRef $tBuffer, $iSize)
	;Create a buffer and header.
	$tBuffer = DllStructCreate(StringFormat("byte[%d]", $iSize))
	$tMidiHdr = DllStructCreate($tag_midihdr)
	$pMidiHdr = DllStructGetPtr($tMidiHdr)
	DllStructSetData($tMidiHdr, "lpData", DllStructGetPtr($tBuffer))
	DllStructSetData($tMidiHdr, "dwBufferLength", DllStructGetSize($tBuffer))
EndFunc   ;==>CreateBuffer

Func SetTiming($hStream, $iPPQ, $iTempo)
	Local $tStrmProp

	;Ensure PPQ is valid
	$iPPQ = BitAND($iPPQ, 0x7FFF)
	If Not $iPPQ Then Return SetError(1)

	;Convert bpm to microseconds per crotchet.
	$iTempo = 60 * 10^6 / $iTempo
	If Not $iTempo Then Return SetError(1)

	;Set timediv property
	$tStrmProp = DllStructCreate("dword;dword")
	DllStructSetData($tStrmProp, 1, 8)
	DllStructSetData($tStrmProp, 2, $iPPQ)
	_midiAPI_StreamProperty($hStream, BitOR($MIDIPROP_SET, $MIDIPROP_TIMEDIV), DllStructGetPtr($tStrmProp))

	;Set tempo property
	DllStructSetData($tStrmProp, 2, $iTempo)
	_midiAPI_StreamProperty($hStream, BitOR($MIDIPROP_SET, $MIDIPROP_TEMPO), DllStructGetPtr($tStrmProp))

EndFunc   ;==>SetTiming

Func midiCallback($hWnd, $iMsg, $hDevice, $pMidiHdr)
	#forceref $hWnd, $iMsg, $hDevice, $pMidiHdr
	Switch $iMsg
		;Reached beat 4 - load the next buffer.
		Case $MM_MOM_POSITIONCB
			Loop($hDevice)

	EndSwitch
EndFunc   ;==>midiCallback
