#include <GDIPlus.au3>
#include "Knobs.au3"
#include <ColorConstants.au3>

; Example how to implement switch and rotary knobs
; copyright P.E. Verbeek, version 1.01
;
; On a rotary knob: click and hold left mouse button and move up/down to dial the knob
; You can browse throught peace.au3 to find out how I used my switch and rotary knobs system
GUI()

Func GUI()
	Local $GUI,$aMsg[2],$FontSize = 8.5,$Knob1,$Knob2,$Knob3,$Knob4,$Knob5,$Knob6,$KnobBeingDialed = -1

	_GDIPlus_StartUp()						; initialize graphics system
	$GUI = GUICreate("Switch and rotary knobs",450,250)
	GUISetFont($FontSize)

	RotaryKnobsInitialize($GUI,"knobdialback.png","knobdialfront.png","knobfront.png",@ScriptDir & "\images",$FontSize)	; initialize rotary knobs system
	; rotary knob "Test"
	$Knob1 = RotaryKnobCreate(20,50,1,0,10,5,0.1,1,-70,70)	; create knob on coordinates 20, 50 initial value 1, range 0 to 10, default value 5, dial pixel resolution 0.1, snap to 1, dialing angle -70 to 70 degrees
	RotaryKnobLabels($Knob1,"Test",0,"testing","0","10","5"); label "Test" 0 = above knob, tooltip "testing", left label "0", right "10", below "5"
	; rotary knob "Echo delay" as used in Peace
	$Knob2 = RotaryKnobCreate(120,50,400,0,1000,500,5,5)
	RotaryKnobLabels($Knob2,"Echo delay %v ms",1,"Echo delay %v ms, default %d, low %l, high %h","0 ms","1000 ms")
	; rotary knob with value change at the right
	$Knob3 = RotaryKnobCreate(220,50,3,0,11,0,0.1,1,-120,120)
	RotaryKnobLabels($Knob3,"Value %v, default %d",2,True,True,True,True)

	SwitchKnobsInitialize($GUI,"switchback.png","switchfront.png","switchfronton.png","switchfrontoff.png",@ScriptDir & "\images",$FontSize,Default,140)	; initialize switch knobs system, red font, text width 140
	SwitchKnobsEnableDisable("switchbackdisabled.png","switchfrontdisabled.png","switchfrontondisabled.png","switchfrontoffdisabled.png")					; initialize enable/disable system
	$Knob5 = SwitchKnobCreate(50,150,1)					; simple on/off switch knob on coordinates 100, 150 initially 1 = on
	SwitchKnobLabels($Knob5,"Switch",4,"Switch show or hide",True,True)
	$Knob4 = SwitchKnobCreate(180,150,1,2,False,True)	; create switch knob on coordinates 100, 150 initially 1 = middle, default 2 = right, false = no on/off switch, true = tri-state switch
	SwitchKnobLabels($Knob4,"Reverse channels %v %d",0,"Reverse channels %v %d","Off","After","Before")	; label "Reverse..."  0 = above knob, tooltip "Reverse..", left label "Off", middle "After", right "Before"
	$Knob6 = SwitchKnobCreate(330,150)
	SwitchKnobLabels($Knob6,"Test %v",0,"Test %v","On","Off")
	SwitchKnobEnable($Knob6,False)						; disable switch knob as example

	GUISetState(@SW_SHOW)
	; initial draw of switch and rotary knobs
	SwitchKnobsDraw()
	RotaryKnobsDraw()
	While 1
		$aMsg = GUIGetMsg(1)
		SwitchKnobsRedraw($aMsg)					; redraw switch knobs on change such as window resizing, moving, etc.
		If $aMsg[0] = $GUI_EVENT_SECONDARYDOWN Then
			SwitchKnobsReset()						; reset switch knob to default on right mouse click
		ElseIf $aMsg[0] = $GUI_EVENT_PRIMARYDOWN Then
			Switch SwitchKnobsScan()
				Case $Knob4
					SwitchKnobEnable($Knob6,SwitchKnobGetValue($Knob4) = 0)
				Case $Knob5
					SwitchKnobShow($Knob6,SwitchKnobGetValue($Knob5))
				Case $Knob6		; enable/disable testing of switch/rotary knobs (by testing the width of the labels are shown in white)
					If SwitchKnobGetValue($Knob6) = 1 Then
						RotaryKnobsTest()
						SwitchKnobsTest()
					Else
						RotaryKnobsTest(False)
						SwitchKnobsTest(False)
					EndIf
					SwitchKnobsDraw()
					RotaryKnobsDraw()
			EndSwitch
		EndIf
		RotaryKnobsRedraw($aMsg)					; redraw rotary knobs on change such as window resizing, moving, etc.
		If $aMsg[0] = $GUI_EVENT_SECONDARYDOWN Then
			ConsoleWrite("Rotary knob " & RotaryKnobsReset() & " value " & RotaryKnobGetValue(RotaryKnobsReset()) & @CRLF)		; reset rotary knob to default on right mouse click
		ElseIf $aMsg[0] = $GUI_EVENT_PRIMARYDOWN Then
			RotaryKnobsScan()						; scan and register which rotary knob is clicked and hold on
		ElseIf $aMsg[0] = $GUI_EVENT_MOUSEMOVE And RotaryKnobDialing() > -1 Then
			RotaryKnobDial()						; let user dial the rotary knob, in other words change its value
			ConsoleWrite("Rotary knob " & RotaryKnobDialing() & " value " & RotaryKnobGetValue(RotaryKnobDialing()) & @CRLF)	; get value of rotary knob being dialed
		EndIf
		If RotaryKnobCheckStopped($aMsg) Then RotaryKnobStopDial()	; check if user has stopped dialing, if so stop the dialing process
		Switch $aMsg[0]
			Case $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd

	GUIDelete($GUI)
	; destroy rotary and switch knobs system
	RotaryKnobsDestroy()
	SwitchKnobsDestroy()
	_GDIPlus_ShutDown()
EndFunc