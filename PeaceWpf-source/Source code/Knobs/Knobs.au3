#include-once
#include <GUIToolTip.au3>
#include <WinAPISys.au3>
#include <GDIPlus.au3>
#include <GUIConstants.au3>
#include <WinAPISysWin.au3>
#include <WinAPIHObj.au3>
#include <WinAPIGdi.au3>

; Graphical Switches and Rotary Knobs
; version 1.17
; copyright by P.E. Verbeek
;
; version history:
; 1.17	Rotary knobs drawing speed improved
;		Switches and rotary knobs drawing quality improved a little bit
; 1.16	A label may show a converted value through %c and passing conversion rate in RotaryKnobCreate()
; 1.15	Times of the showing of tooltips of switch and rotary knobs can be adjusted
; 1.14	Bug: Double enabling a switch or rotary knob would redraw the background of the controlling label (transparency was gone?)
; 1.13	For drawing of switch and rotary knobs the window background color is always retrieved instead of once
; 1.12	RotaryKnobCreate(): the snap to value parameter can be an array of possible values
; 1.11	Support added for more than 1 GUI
;		SwitchKnobsAddGUI() and RotaryKnobsAddGUI() to add another GUI for switch or rotary knobs
;		SwitchKnobsDraw() and RotaryKnobsDraw() changed: parameter added for GUI number (0 = first, 1 = second, etc.)
;		SwitchKnobsGetGUI() and RotaryKnobsGetGUI() to get GUI handle by GUI number (0 = first, 1 = second, etc.)
;		SwitchKnobsGetGraphic() and RotaryKnobsGetGraphic() to get GDI plus graphic handle by GUI number (0 = first, 1 = second, etc.)
;		SwitchKnobGetGUI() and RotaryKnobGetGUI(): deprecated because wrong function name (see above)
;		SwitchKnobsIsGUI() and RotaryKnobsIsGUI() to check if given GUI is a switch or rotary knobs GUI
;		SwitchKnobsResetAll() and RotaryKnobsResetAll() changed: parameter added for GUI number (-1 = all GUIs, 0 = first, 1 = second, etc.)
;		Bug: Test background wasn't drawn on bottom label for rotary knobs
;		Bug: SwitchKnobEnable() and RotaryKnobEnable() didn't consider none used labels
; 1.10	SwitchKnobsLabelsDisabledColor() and SwitchKnobsLabelsColor() to color labels
;		RotaryKnobsLabelsDisabledColor() and RotaryKnobsLabelsColor() to color labels
; 1.09	SwitchKnobsTest() and RotaryKnobsTest() accept a color for label showing
; 1.08	SwitchKnobsLabelsDisabled() and RotaryKnobsLabelsDisabled() to show labels as disabled font or normal font
; 1.07	Clearing area of switch and rotary knob increased by 1 pixel on each side, needed for erasing any pixels of previous drawn knob
; 1.06	Local variable removed which isn't needed for a For loop
; 1.05	RotaryKnobsOnOff(): new argument for off mechanisme and image
;		RotaryKnobSwitch() to switch knob on/off
;		RotaryKnobToggle() to toggle knob on/off
;		RotaryKnobOn() to get if knob is switched off
;		RotaryKnobIncreaseValue() and RotaryKnobDecreaseValue(): 2 arguments for checking on (other than default value) state and off state
; 1.04	Draw system improved: knobs are only redrawn if necessary
;		Draw system improved: knobs are correctly redrawn at a window resize
;		SwitchKnobShow() and RotaryKnobShow() added to show or hide the knob
; 		SwitchKnobEnabled() added
; 		SwitchKnobEnable() and RotaryKnobEnable() perform a redraw. Own redraw no longer required
;		SwitchKnobsScanSize() and RotaryKnobsScanSize() added to enlarge (or decrease) scan surface size
; 		Controls fixated on their positions when window resizing. This behaviour can initially be set to false
; 1.03	Width of bottom switch label enlarged to width main label
; 1.02	More comments added
; 1.01	Initial release version
;
; List of functions
;	SwitchKnobsInitialize			Initialize the use of switch knobs
;	SwitchKnobsAddGUI				Add another GUI for switch knobs
;	SwitchKnobsGetGUI				Get GUI handle by GUI number
;	SwitchKnobsIsGUI				Check if given GUI is a switch knobs GUI
;	SwitchKnobsGetGraphic			Get GDI plus graphic handle by GUI number
;	SwitchKnobsScanSize				Set the scanning knob surface size
;	SwitchKnobsEnableDisable		Initialize the enable/disable switch knobs feature
;	SwitchKnobsLabelsDisabled		Set if switch labels are shown as disabled
;	SwitchKnobsLabelsDisabledColor	Set color of disabled switch knobs (color for enabled knobs must be provided)
;	SwitchKnobsLabelsColor			Set color of switch knobs
;	SwitchKnobsTest					Test switch knobs (showing label lengths by different background color)
;	SwitchKnobsDestroy				Clean up GDI objects
;	SwitchKnobsScan					Scan and register switch knob user is hovering above (if any)
;	SwitchKnobsRedraw				Force redraw of all switch knobs
;	SwitchKnobsResetAll				Reset all switch knobs to default
;	SwitchKnobsReset				Reset switch knob user hovering above to default value
;	SwitchKnobsDraw					Draw all switch knobs
;	SwitchKnobCreate				Create a switch knob
;	SwitchKnobLabels				Adding labels to a switch knob
;	SwitchKnobEnable				Enable or disable switch knob
;	SwitchKnobEnabled				Get if switch knob is enabled or disabled
;	SwitchKnobShow					Show or hide switch knob
;	SwitchKnobDraw					Draw a switch knob
;	SwitchKnobFlip					Flip a switch
;	SwitchKnobReset					Reset switch knob to default value
;	SwitchKnobSetValue				Set switch knob value
;	SwitchKnobGetValue				Get switch knob value
;	SwitchKnobGetDefaultValue		Get switch knob default value
;	SwitchKnobGetString				Get switch knob value or label

;	RotaryKnobsInitialize			Initialize the use of rotary knobs
;	RotaryKnobsAddGUI				Add another GUI for rotary knobs
;	RotaryKnobsGetGUI				Get GUI handle by GUI number
;	RotaryKnobsIsGUI				Check if given GUI is a rotary knobs GUI
;	RotaryKnobsGetGraphic			Get GDI plus graphic handle by GUI number
;	RotaryKnobsScanSize				Set the scanning knob surface size
;	RotaryKnobsEnableDisable		Initialize the enable/disable rotary knobs feature
;	RotaryKnobsLabelsDisabled		Set if rotary labels are shown as disabled
;	RotaryKnobsLabelsDisabledColor	Set color of disabled rotary knobs (color for enabled knobs must be provided)
;	RotaryKnobsLabelsColor			Set color of rotary knobs
;	RotaryKnobsOnOff 				Set file images to show "on" by other value than default value and "off" by user switch off
;	RotaryKnobsTest					Test rotary knobs (showing label lengths by different background color)
;	RotaryKnobsDestroy				Clean up GDI objects
;	RotaryKnobsScan					Scan and register rotary knob user is hovering above (if any)
;	RotaryKnobsRedraw				Force redraw of all rotary knobs
;	RotaryKnobsResetAll				Reset all rotary knobs to default
;	RotaryKnobsReset				Reset rotary knob user hovering above to default value
;	RotaryKnobsDraw					(Re)draw all rotary knobs
;	RotaryKnobCreate				Create a rotary knob
;	RotaryKnobLabels				Adding labels to a rotary knob
;	RotaryKnobEnable				Enable or disable rotary knob
;	RotaryKnobEnabled				Get if rotary knob is enabled or disabled
;	RotaryKnobOn					Get if rotary knob is switch on or off
;	RotaryKnobShow					Show or hide rotary knob
;	RotaryKnobDraw					(Re)draw a rotary knob
;	RotaryKnobDial					Let user dial selected rotary knob
;	RotaryKnobDialing				Current rotary knob being dialed
;	RotaryKnobCheckStopped			Check if user has stopped dailing
;	RotaryKnobStopDial				User stopped dialing, so reset for the next dial
;	RotaryKnobReset					Reset rotary knob to default value
;	RotaryKnobSwitch				Switch rotary knob on or off
;	RotaryKnobToggle				Toggle rotary knob on or off
;	RotaryKnobSetValue				Set rotary knob value
;	RotaryKnobIncreaseValue			Increase rotary knob value
;	RotaryKnobDecreaseValue			Decrease rotary knob value
;	RotaryKnobGetValue				Get rotary knob value
;	RotaryKnobGetDefaultValue		Get rotary knob default value

; Initialize the use of switch knobs
; SwitchKnobsInitialize(
;	$GUI							Handle of GUI
;	$FileSwitchBack					File name of switch background image
;	$FileSwitchFront				File name of switch foreground image
;	$FileSwitchFrontOn				File name of switch on foreground image
;	$FileSwitchFrontOff				File name of switch off foreground image
;	$ImagesDirectory = @ScriptDir	Directory of image files
;	$FontSize = 8.5					Font size of GUI
;	$FontColor = -1					Font color, default GUI font color
;	$TextWidth = 140				Text width of switch
;	$LabelWidth = 60				Label width of switch examples: "on", "off"
;	$LabelMargin = 6				Margin between labels and background image
;	$ControlsFixed = True			Controls are fixed on their positions or repositioned at a window size change
; )
; Return values
;	0								Successful
;	-1								Images directory doesn't exist
;	-2								Knob background image file doesn't exist
;	-3								Knob foreground image file doesn't exist
;	-4								Knob on image file doesn't exist
;	-5								Knob off image file doesn't exist
Func SwitchKnobsInitialize($GUI,$FileSwitchBack,$FileSwitchFront,$FileSwitchFrontOn,$FileSwitchFrontOff,$ImagesDirectory = @ScriptDir,$FontSize = 8.5,$FontColor = -1,$TextWidth = 140,$LabelWidth = 60,$LabelMargin = 6,$ControlsFixed = True)
	Global $_SwitchKnobs,$_SwitchKnobsHandle,$_SwitchKnobsDir,$_SwitchKnobsBack,$_SwitchKnobsFront,$_SwitchKnobsFrontOn,$_SwitchKnobsFrontOff
	Global $_SwitchKnobsBackDisabled = -1,$_SwitchKnobsFrontDisabled = -1,$_SwitchKnobsFrontOnDisabled = -1,$_SwitchKnobsFrontOffDisabled = -1,$_SwitchKnobsScanSize
	Global $_SwitchKnobsFontSize,$_SwitchKnobsFontColor,$_SwitchKnobsLabelMargin,$_SwitchKnobsControlsFixed,$_SwitchKnobsTextWidth,$_SwitchKnobsLabelWidth,$_SwitchKnobsWidth,$_SwitchKnobsHeight
	Global $_SwitchKnobsEnabled,$_SwitchKnobsLabelsDisabled,$_SwitchKnobsFontDisabledColor,$_SwitchKnobsTest,$_SwitchKnobsTestColor,$_SwitchKnobsTextValue,$_SwitchKnobsTextDefaultValue

	_SwitchKnobsAddGUI($GUI,True)
	$_SwitchKnobs = False
	$_SwitchKnobsHandle = 0
	$_SwitchKnobsDir = $ImagesDirectory
	$_SwitchKnobsBack = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchBack)
	$_SwitchKnobsWidth = _GDIPlus_ImageGetWidth($_SwitchKnobsBack)
	$_SwitchKnobsHeight = _GDIPlus_ImageGetHeight($_SwitchKnobsBack)
	$_SwitchKnobsFront = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFront)
	$_SwitchKnobsFrontOn = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFrontOn)
	$_SwitchKnobsFrontOff = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFrontOff)
	$_SwitchKnobsFontSize = $FontSize
	$_SwitchKnobsFontColor = $FontColor
	$_SwitchKnobsTextWidth = Round($TextWidth*$FontSize/8.5)
	$_SwitchKnobsLabelWidth = Round($LabelWidth*$FontSize/8.5)
	$_SwitchKnobsLabelMargin = $LabelMargin
	$_SwitchKnobsControlsFixed = $ControlsFixed
	$_SwitchKnobsTextValue = "%v"
	$_SwitchKnobsTextDefaultValue = "%d"
	$_SwitchKnobsEnabled = False
	$_SwitchKnobsLabelsDisabled = True
	$_SwitchKnobsFontDisabledColor = -1
	$_SwitchKnobsScanSize = 1
	$_SwitchKnobsTest = False
	$_SwitchKnobsTestColor = 0xFFFFFF
	If Not FileExists($ImagesDirectory) Then Return -1
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchBack) Then Return -2
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFront) Then Return -3
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFrontOn) Then Return -4
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFrontOff) Then Return -5
	Return 0
EndFunc

; Add another GUI for switch knobs
; SwitchKnobsAddGUI(
;	$GUI							Handle of GUI
;	$InitialTime = 500 ms			Initial delay
;	$AutoPopTime = 5000 ms			Time of showing
;	$ReshowTime = 100 ms			show delay between controls
; )
Func SwitchKnobsAddGUI($GUI,$InitialTime = 500,$AutoPopTime = 5000, $ReshowTime = 100)
	_SwitchKnobsAddGUI($GUI,False,$InitialTime,$AutoPopTime,$ReshowTime)
EndFunc

Func _SwitchKnobsAddGUI($GUI,$First = False,$InitialTime = 500,$AutoPopTime = 5000,$ReshowTime = 100)	; Internal function to add a GUI for switch knobs
	Local $GUIPosition,$GUIno

	If $First Then
		Global $_SwitchKnobsGUI[1][8]
	Else
		ReDim $_SwitchKnobsGUI[UBound($_SwitchKnobsGUI)+1][8]
	EndIf
	$GUIno = UBound($_SwitchKnobsGUI)-1
	$_SwitchKnobsGUI[$GUIno][0] = $GUI
	_SwitchKnobsCreateGraphicObject(UBound($_SwitchKnobsGUI)-1)
	$GUIPosition = WinGetPos($GUI)
	; correct for minimize state
	If $GUIPosition[0] = -32000 Then $GUIPosition[0] = 0
	If $GUIPosition[1] = -32000 Then $GUIPosition[1] = 0
	$_SwitchKnobsGUI[$GUIno][2] = $GUIPosition[0]
	$_SwitchKnobsGUI[$GUIno][3] = $GUIPosition[1]
	$_SwitchKnobsGUI[$GUIno][4] = $GUIPosition[2]
	$_SwitchKnobsGUI[$GUIno][5] = $GUIPosition[3]
	$_SwitchKnobsGUI[$GUIno][6] = _GUIToolTip_Create($GUI)
	_GUIToolTip_SetMaxTipWidth($_SwitchKnobsGUI[$GUIno][6],1000)
	If $InitialTime = -1 Then
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_AUTOMATIC,-1)
	Else
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_INITIAL,$InitialTime)
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_AUTOPOP,$AutoPopTime)
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_RESHOW,$ReshowTime)
	EndIf
	$_SwitchKnobsGUI[$GUIno][7] = 0	; clear brush
EndFunc

; Set times of tooltips
; SwitchKnobsToolTipsTimes(
;	$GUIno							Number of GUI, 0 = first
;	$InitialTime = 500				Initial delay for showing
;	$AutoPopTime = 5000				Total time of showing
;	$ReshowTime = 100				Delay to next control
; )
Func SwitchKnobsToolTipsTimes($GUIno,$InitialTime = 500,$AutoPopTime = 5000, $ReshowTime = 100)
	If $InitialTime = -1 Then
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_AUTOMATIC,-1)
	Else
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_INITIAL,$InitialTime)
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_AUTOPOP,$AutoPopTime)
		_GUIToolTip_SetDelayTime($_SwitchKnobsGUI[$GUIno][6],$TTDT_RESHOW,$ReshowTime)
	EndIf
EndFunc

; Get GUI handle by GUI number
; SwitchKnobsGetGUI(
;	$GUIno							Number of GUI, 0 = first
; )
Func SwitchKnobsGetGUI($GUIno = 0)
	Return $_SwitchKnobsGUI[$GUIno][0]
EndFunc

; Check if given GUI is a switch knobs GUI
; SwitchKnobsIsGUI(
;	$GUI							Handle of GUI
; )
; Return values
;	True							Handle is a switch knobs GUI
;	False							Not
Func SwitchKnobsIsGUI($GUI)
	For $GUIno = 0 To UBound($_SwitchKnobsGUI)-1
		If $_SwitchKnobsGUI[$GUIno][0] = $GUI Then Return True
	Next
	Return False
EndFunc

; Get GDI plus graphic handle by GUI number
; SwitchKnobsGetGraphic(
;	$GUIno							Number of GUI, 0 = first
; )
Func SwitchKnobsGetGraphic($GUIno = 0)
	Return $_SwitchKnobsGUI[$GUIno][1]
EndFunc

Func _SwitchKnobsCreateGraphicObject($GUIno)	; Internal function to create graphics object
	$_SwitchKnobsGUI[$GUIno][1] = _GDIPlus_GraphicsCreateFromHWND($_SwitchKnobsGUI[$GUIno][0])
	_GDIPlus_GraphicsSetInterpolationMode($_SwitchKnobsGUI[$GUIno][1],$GDIP_INTERPOLATIONMODE_HIGHQUALITYBICUBIC)
EndFunc

Func _SwitchKnobActiveWindow()				; Internal function to detect an active switch knobs window
	If Not IsArray($_SwitchKnobs) Then Return -1
	For $GUIno = 0 To UBound($_SwitchKnobsGUI)-1
		If WinActive($_SwitchKnobsGUI[$GUIno][0]) Then Return $GUIno
	Next
	Return -1
EndFunc

; Set the scanning knob surface size
; SwitchKnobsScanSize(
;	$SizeFactor = 1					Scan surface width/height is multiplied by this factor
; )
; Return Value
;	Previous size factor
Func SwitchKnobsScanSize($SizeFactor = 1)
	Local $Factor = $_SwitchKnobsScanSize
	$_SwitchKnobsScanSize = $SizeFactor
	Return $Factor
EndFunc

; Initialize the enable/disable switch knobs feature
; SwitchKnobsEnableDisable(
; 	$FileSwitchBackDisabled			File name of disabled switch background image
; 	$FileSwitchFrontDisabled		File name of disabled switch foreground image
; 	$FileSwitchFrontOnDisabled		File name of disabled switch on foreground image
; 	$FileSwitchFrontOffDisabled		File name of disabled switch off foreground image
; 	$Enable = True
; )
; Return values
;	True or False					Enabled/disabled feature set to on or off
;	-1								Images directory doesn't exist
;	-2								Disabled knob background image file doesn't exist
;	-3								Disabled knob foreground image file doesn't exist
;	-4								Disabled knob on image file doesn't exist
;	-5								Disabled knob off image file doesn't exist
Func SwitchKnobsEnableDisable($FileSwitchBackDisabled,$FileSwitchFrontDisabled,$FileSwitchFrontOnDisabled,$FileSwitchFrontOffDisabled,$Enable = True)	; enable disabling of switch knobs
	$_SwitchKnobsEnabled = $Enable
	If IsBool($FileSwitchBackDisabled) Then $_SwitchKnobsEnabled = $FileSwitchBackDisabled
	$_SwitchKnobsBackDisabled = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchBackDisabled)
	$_SwitchKnobsWidthDisabled = _GDIPlus_ImageGetWidth($_SwitchKnobsBackDisabled)
	$_SwitchKnobsHeightDisabled = _GDIPlus_ImageGetHeight($_SwitchKnobsBackDisabled)
	$_SwitchKnobsFrontDisabled = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFrontDisabled)
	$_SwitchKnobsFrontOnDisabled = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFrontOnDisabled)
	$_SwitchKnobsFrontOffDisabled = _GDIPlus_ImageLoadFromFile($_SwitchKnobsDir & "\" & $FileSwitchFrontOffDisabled)
	If Not FileExists($_SwitchKnobsDir) Then
		$_SwitchKnobsEnabled = False
		Return -1
	EndIf
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchBackDisabled) Then
		$_SwitchKnobsEnabled = False
		Return -2
	EndIf
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFrontDisabled) Then
		$_SwitchKnobsEnabled = False
		Return -3
	EndIf
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFrontOnDisabled) Then
		$_SwitchKnobsEnabled = False
		Return -4
	EndIf
	If Not FileExists($_SwitchKnobsDir & "\" & $FileSwitchFrontOffDisabled) Then
		$_SwitchKnobsEnabled = False
		Return -5
	EndIf
	Return $_SwitchKnobsEnabled
EndFunc

; Set if switch labels are shown as disabled
; SwitchKnobsLabelsDisabled(
;	$Disabled = True
; )
Func SwitchKnobsLabelsDisabled($Disabled = True)
	$_SwitchKnobsLabelsDisabled = $Disabled
EndFunc

; Set color of disabled switch knobs (color for enabled knobs must be provided)
; SwitchKnobsLabelsDisabledColor(
;	$Color = -1						set -1 to reset
; )
Func SwitchKnobsLabelsDisabledColor($Color = -1)
	$_SwitchKnobsFontDisabledColor = $Color
EndFunc

; Set color of switch knobs
; SwitchKnobsLabelsColor(
;	$Color = -1						set -1 to reset
; )
Func SwitchKnobsLabelsColor($Color = -1)
	$_SwitchKnobsFontColor = $Color
EndFunc

; Test switch knobs (showing label lengths by different background color)
; SwitchKnobsTest(
;	$Test = True					set testing on or off
;	$Color = 0xFFFFFF				set color of labels
; )
Func SwitchKnobsTest($Test = True,$Color = 0xFFFFFF)
	$_SwitchKnobsTest = $Test
	$_SwitchKnobsTestColor = $Color
EndFunc

; Clean up GDI objects
Func SwitchKnobsDestroy()
	If Not IsArray($_SwitchKnobs) Then Return False

	For $GUIno = 0 To UBound($_SwitchKnobsGUI)-1
		_GDIPlus_GraphicsDispose($_SwitchKnobsGUI[$GUIno][1])
		If $_SwitchKnobsGUI[$GUIno][7] <> 0 Then _GDIPlus_BrushDispose($_SwitchKnobsGUI[$GUIno][7])
	Next
	_GDIPlus_ImageDispose($_SwitchKnobsBack)
	_GDIPlus_ImageDispose($_SwitchKnobsFront)
	_GDIPlus_ImageDispose($_SwitchKnobsFrontOn)
	_GDIPlus_ImageDispose($_SwitchKnobsFrontOff)
	If $_SwitchKnobsEnabled Then
		_GDIPlus_ImageDispose($_SwitchKnobsBackDisabled)
		_GDIPlus_ImageDispose($_SwitchKnobsFrontDisabled)
		_GDIPlus_ImageDispose($_SwitchKnobsFrontOnDisabled)
		_GDIPlus_ImageDispose($_SwitchKnobsFrontOffDisabled)
	EndIf
	Return True
EndFunc

; Scan and register switch knob user is hovering above (if any)
; SwitchKnobsScan(
;	$HoverOnly = False				Hover only, don't register
; )
; Return values
;	Knob handle						Knob user is hovering above
;	-1								User isn't hovering above a knob
Func SwitchKnobsScan($HoverOnly = False)
	Local $GUIno = _SwitchKnobActiveWindow()
	$_SwitchKnob = -1
	If $GUIno = -1 Then Return -1

	Local $aCursor
	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][23] <> $GUIno Then ContinueLoop
		$aCursor = GUIGetCursorInfo($_SwitchKnobsGUI[$GUIno][0])
		If $aCursor[0] >= $_SwitchKnobs[$KnobNo][1]+(0.5-0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsWidth And $aCursor[0] <= $_SwitchKnobs[$KnobNo][1]+(0.5+0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsWidth And $aCursor[1] >= $_SwitchKnobs[$KnobNo][2]+(0.5-0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsHeight And $aCursor[1] <= $_SwitchKnobs[$KnobNo][2]+(0.5+0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsHeight Then
			If Not $HoverOnly And (Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20]) Then _SwitchKnobFlip($KnobNo)
			Return $_SwitchKnobs[$KnobNo][0]
		EndIf
	Next
	Return -1
EndFunc

; Force redraw of all switch knobs
; SwitchKnobsRedraw(
; 	$aMsg							GUI message array
; )
; Return value
;	True							Redrawn
;	False							No redraw was needed
Func SwitchKnobsRedraw($aMsg)
	Local $GUI = -1,$Graphic
	For $GUIno = 0 To UBound($_SwitchKnobsGUI)-1
		If $aMsg[1] = $_SwitchKnobsGUI[$GUIno][0] Then
			$GUI = $GUIno
			ExitLoop
		EndIf
	Next
	If $GUI = -1 Then Return False
	If $aMsg[0] = $GUI_EVENT_RESTORE Or $aMsg[0] = $GUI_EVENT_MAXIMIZE Or $aMsg[0] = $GUI_EVENT_RESIZED Then
		If $aMsg[0] = $GUI_EVENT_MAXIMIZE Or $aMsg[0] = $GUI_EVENT_RESIZED Then	; create new graphic object because window has been resized
			_GDIPlus_GraphicsDispose($_SwitchKnobsGUI[$GUI][1])
			_SwitchKnobsCreateGraphicObject($GUI)
		EndIf
		SwitchKnobsDraw($GUIno)
		Return True
	ElseIf Not BitAND(WinGetState($_SwitchKnobsGUI[$GUI][0]),16) Then		; window isn't minimized so check
		Local $Position = WinGetPos($_SwitchKnobsGUI[$GUI][0])
		If $Position[0] <> $_SwitchKnobsGUI[$GUI][2] Or $Position[1] <> $_SwitchKnobsGUI[$GUI][3] Or $Position[2] <> $_SwitchKnobsGUI[$GUI][4] Or $Position[3] <> $_SwitchKnobsGUI[$GUI][5] Then
			$_SwitchKnobsGUI[$GUI][2] = $Position[0]
			$_SwitchKnobsGUI[$GUI][3] = $Position[1]
			If $Position[2] <> $_SwitchKnobsGUI[$GUI][4] Or $Position[3] <> $_SwitchKnobsGUI[$GUI][5] Then	; create new graphic object because window has been resized
				_GDIPlus_GraphicsDispose($_SwitchKnobsGUI[$GUI][1])
				_SwitchKnobsCreateGraphicObject($GUI)
			EndIf
			$_SwitchKnobsGUI[$GUI][4] = $Position[2]
			$_SwitchKnobsGUI[$GUI][5] = $Position[3]
			SwitchKnobsDraw($GUIno)
			Return True
		EndIf
	EndIf
	Return False
EndFunc

; Reset all switch knobs to default
; SwitchKnobsResetAll(
;	$GUIno							Number of GUI, reset -1 = all switch knoba GUIs, 0 = first
; )
Func SwitchKnobsResetAll($GUIno = -1)
	If _SwitchKnobActiveWindow() = -1 Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $GUIno = -1 Or ($GUIno > -1 And $_SwitchKnobs[$KnobNo][23] = $GUIno) Then SwitchKnobReset($_SwitchKnobs[$KnobNo][0])
	Next
	Return True
EndFunc

; Reset switch knob user hovering above to default value
; Return values
;
; Return values
;	Knob handle						Knob user is hovering above
;	-1								User isn't hovering above a knob
Func SwitchKnobsReset()
	Local $GUIno = _SwitchKnobActiveWindow()
	$_SwitchKnob = -1
	If $GUIno = -1 Then Return -1

	Local $aCursor
	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][23] <> $GUIno Then ContinueLoop
		$aCursor = GUIGetCursorInfo($_SwitchKnobsGUI[$GUIno][0])
		If $aCursor[0] >= $_SwitchKnobs[$KnobNo][1]+(0.5-0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsWidth And $aCursor[0] <= $_SwitchKnobs[$KnobNo][1]+(0.5+0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsWidth And $aCursor[1] >= $_SwitchKnobs[$KnobNo][2]+(0.5-0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsHeight And $aCursor[1] <= $_SwitchKnobs[$KnobNo][2]+(0.5+0.5*$_SwitchKnobsScanSize)*$_SwitchKnobsHeight Then
			If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then SwitchKnobReset($_SwitchKnobs[$KnobNo][0])
			Return $_SwitchKnobs[$KnobNo][0]
		EndIf
	Next
	Return -1
EndFunc

; Draw all switch knobs
Func SwitchKnobsDraw($GUIno = 0)
	If Not IsArray($_SwitchKnobs) Then Return False

	Local $DC = _WinAPI_GetDC($_SwitchKnobsGUI[$GUIno][0]),$BackColor = 0
	If $DC <> 0 Then
		$BackColor = _WinAPI_GetBkColor($DC)
		_WinAPI_ReleaseDC($_SwitchKnobsGUI[$GUIno][0],$DC)
		If $BackColor = -1 Then
			$BackColor = _WinAPI_GetSysColor(4)				; color probably same as background color of popup menu $COLOR_MENU ($COLOR_WINDOW gives wrong color)
			If $BackColor = 0 Then $BackColor = 15790320	; fail save in case black is returned
		EndIf
	EndIf
	If $_SwitchKnobsGUI[$GUIno][7] <> 0 Then _GDIPlus_BrushDispose($_SwitchKnobsGUI[$GUIno][7])
	$_SwitchKnobsGUI[$GUIno][7] = _GDIPlus_BrushCreateSolid(0xFF000000+$BackColor)
	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][23] <> $GUIno Then ContinueLoop
		_SwitchKnobDraw($KnobNo)
	Next
	Return True
EndFunc

; Create a switch knob
; SwitchKnobCreate(
;	$X,$Y							Position of switch knob
;	$Value = 0						0, 1 or 2, usually 0 = off, 1 = on
;	$DefaultValue = 0				Default value of switch
;	$OnOffSwitch = true				On/Off switch
;	$TriSwitch = false				3 states switch
; )
; Return value						Knob handle
Func SwitchKnobCreate($X,$Y,$Value = 0,$DefaultValue = 0,$OnOffSwitch = True,$TriSwitch = False)		; create a switch
	Local $KnobNo,$KnobXOffset = 2

	If Not IsArray($_SwitchKnobs) Then
		$KnobNo = 0
		Global $_SwitchKnobs[1][25]
	Else
		$KnobNo = UBound($_SwitchKnobs)
		ReDim $_SwitchKnobs[$KnobNo+1][25]
	EndIf
	$_SwitchKnobsHandle += 1
	$_SwitchKnobs[$KnobNo][0] = $_SwitchKnobsHandle
	$_SwitchKnobs[$KnobNo][1] = $X
	$_SwitchKnobs[$KnobNo][2] = $Y
	$_SwitchKnobs[$KnobNo][3] = $X+$_SwitchKnobsWidth-$KnobXOffset-_GDIPlus_ImageGetWidth($_SwitchKnobsFront)
	$_SwitchKnobs[$KnobNo][4] = $X+$KnobXOffset+1
	$_SwitchKnobs[$KnobNo][5] = $X+$_SwitchKnobsWidth/2-_GDIPlus_ImageGetWidth($_SwitchKnobsFront)/2
	$_SwitchKnobs[$KnobNo][6] = $Y+$_SwitchKnobsHeight/2-_GDIPlus_ImageGetHeight($_SwitchKnobsFront)/2
	If $Value < 0 Then
		$Value = 0
	ElseIf $TriSwitch Then
		If $Value > 2 Then $Value = 2
	ElseIf $Value > 1 Then
		$Value = 1
	EndIf
	$_SwitchKnobs[$KnobNo][7] = $Value
	$_SwitchKnobs[$KnobNo][8] = $DefaultValue
	$_SwitchKnobs[$KnobNo][9] = $OnOffSwitch
	$_SwitchKnobs[$KnobNo][10] = $TriSwitch
	$_SwitchKnobs[$KnobNo][11] = GUICtrlCreateLabel("",$X,$Y,1,2*$_SwitchKnobsFontSize)
	GUICtrlSetState(-1,$GUI_HIDE+$GUI_DISABLE)
	If $_SwitchKnobsControlsFixed Then GUICtrlSetResizing($_SwitchKnobs[$KnobNo][11],$GUI_DOCKALL)
	GUICtrlSetBkColor($_SwitchKnobs[$KnobNo][11],$_SwitchKnobsTest ? $_SwitchKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
	GUICtrlSetFont(-1,$_SwitchKnobsFontSize)
	If $_SwitchKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_SwitchKnobsFontColor)
	$_SwitchKnobs[$KnobNo][12] = False
	$_SwitchKnobs[$KnobNo][13] = ""
	$_SwitchKnobs[$KnobNo][14] = GUICtrlCreateLabel("",$X,$Y,$_SwitchKnobsWidth,$_SwitchKnobsHeight)	; 3.3.18.0: knob drawn to early, before label is drawn
;~ 	$_SwitchKnobs[$KnobNo][14] = GUICtrlCreateGraphic($X,$Y,$_SwitchKnobsWidth,$_SwitchKnobsHeight)	; works, test under 3.3.18.0
;~ 	$_SwitchKnobs[$KnobNo][14] = GUICtrlCreatePic("",$X,$Y,$_SwitchKnobsWidth,$_SwitchKnobsHeight)	; works, test under 3.3.18.0
	GUICtrlSetBkColor(-1,$GUI_BKCOLOR_TRANSPARENT)
	If $_SwitchKnobsControlsFixed Then GUICtrlSetResizing($_SwitchKnobs[$KnobNo][14],$GUI_DOCKALL)
	$_SwitchKnobs[$KnobNo][15] = False
	$_SwitchKnobs[$KnobNo][16] = ""
	$_SwitchKnobs[$KnobNo][17] = -1
	$_SwitchKnobs[$KnobNo][18] = -1
	$_SwitchKnobs[$KnobNo][19] = -1
	$_SwitchKnobs[$KnobNo][20] = True		; default knob enabled
	$_SwitchKnobs[$KnobNo][21] = True		; default knob shown or hidden
	$_SwitchKnobs[$KnobNo][22] = False		; if true knob is drawn
	$_SwitchKnobs[$KnobNo][23] = UBound($_SwitchKnobsGUI)-1
	$_SwitchKnobs[$KnobNo][24] = $_SwitchKnobsGUI[UBound($_SwitchKnobsGUI)-1][0]
	Return $_SwitchKnobs[$KnobNo][0]
EndFunc

; Adding labels to a switch knob
; SwitchKnobLabels(
;	$Knob							Handle of the switch knob
;	$Label = ""						Title/description string
;	$LabelPosition					Position of label, 0 = top, 1 = right, 2 = left, 3 = bottom high, 4 = bottom low
;	$Tooltip = ""					Tooltip string
;	$Left = ""						Left label string
;	$Right = ""						Right label string
;	$Bottom = ""					$Bottom label string
; )
; Return values
;	True							Knob exists
;	False							Knob doesn't exist
Func SwitchKnobLabels($Knob,$Label = "",$LabelPosition = 0,$Tooltip = "",$Left = "",$Right = "",$Bottom = "")	; add labels
	If Not IsArray($_SwitchKnobs) Then Return False

	Local $X,$Y
	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			If StringLen($Label) > 0 Then
				GUICtrlSetState($_SwitchKnobs[$KnobNo][11],$GUI_ENABLE+$GUI_SHOW)
				$_SwitchKnobs[$KnobNo][12] = True
				$_SwitchKnobs[$KnobNo][13] = $Label
				Switch $LabelPosition
					Case 1		; right
						GUICtrlSetPos($_SwitchKnobs[$KnobNo][11],$_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsLabelMargin+3+$_SwitchKnobsWidth,$_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight/2-0.8*$_SwitchKnobsFontSize,$_SwitchKnobsTextWidth,2*$_SwitchKnobsFontSize)
						GUICtrlSetStyle($_SwitchKnobs[$KnobNo][11],$SS_LEFT)
					Case 2		; left
						GUICtrlSetPos($_SwitchKnobs[$KnobNo][11],$_SwitchKnobs[$KnobNo][1]+3-$_SwitchKnobsLabelMargin-$_SwitchKnobsTextWidth,$_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight/2-0.8*$_SwitchKnobsFontSize,$_SwitchKnobsTextWidth,2*$_SwitchKnobsFontSize)
						GUICtrlSetStyle($_SwitchKnobs[$KnobNo][11],$SS_RIGHT)
					Case 3		; bottom high
						GUICtrlSetPos($_SwitchKnobs[$KnobNo][11],$_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsWidth/2-$_SwitchKnobsTextWidth/2,$_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight+$_SwitchKnobsLabelMargin+0.8*$_SwitchKnobsFontSize,$_SwitchKnobsTextWidth,2*$_SwitchKnobsFontSize)
						GUICtrlSetStyle($_SwitchKnobs[$KnobNo][11],$SS_CENTER)
					Case 4		; bottom low
						GUICtrlSetPos($_SwitchKnobs[$KnobNo][11],$_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsWidth/2-$_SwitchKnobsTextWidth/2,$_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight+$_SwitchKnobsLabelMargin-0.2*$_SwitchKnobsFontSize,$_SwitchKnobsTextWidth,2*$_SwitchKnobsFontSize)
						GUICtrlSetStyle($_SwitchKnobs[$KnobNo][11],$SS_CENTER)
					Case Else	; top
						GUICtrlSetPos($_SwitchKnobs[$KnobNo][11],$_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsWidth/2-$_SwitchKnobsTextWidth/2,$_SwitchKnobs[$KnobNo][2]-$_SwitchKnobsLabelMargin-1.2*$_SwitchKnobsFontSize,$_SwitchKnobsTextWidth,2*$_SwitchKnobsFontSize)
						GUICtrlSetStyle($_SwitchKnobs[$KnobNo][11],$SS_CENTER)
				EndSwitch
			EndIf
			If IsBool($Tooltip) Then $Tooltip = $_SwitchKnobsTextValue
			If StringLen($Tooltip) > 0 Then
				$_SwitchKnobs[$KnobNo][15] = True
				$_SwitchKnobs[$KnobNo][16] = $Tooltip
				_GUIToolTip_AddTool($_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][6],0,$Tooltip,GUICtrlGetHandle($_SwitchKnobs[$KnobNo][14]))
			EndIf
			If IsBool($Left) Then $Left = "Off"
			If StringLen($Left) > 0 Then
				$X = $_SwitchKnobs[$KnobNo][1]-$_SwitchKnobsLabelMargin-$_SwitchKnobsLabelWidth+3
				$Y = $_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight/2-0.8*$_SwitchKnobsFontSize
				$_SwitchKnobs[$KnobNo][17] = GUICtrlCreateLabel($Left,$X,$Y,$_SwitchKnobsLabelWidth,-1,$SS_RIGHT)
				If $_SwitchKnobsControlsFixed Then GUICtrlSetResizing($_SwitchKnobs[$KnobNo][17],$GUI_DOCKALL)
				GUICtrlSetBkColor($_SwitchKnobs[$KnobNo][17],$_SwitchKnobsTest ? $_SwitchKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_SwitchKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_SwitchKnobsFontColor)
				GUICtrlSetFont(-1,$_SwitchKnobsFontSize-1)
			EndIf
			If IsBool($Right) Then $Right = "On"
			If StringLen($Right) > 0 Then
				$X = $_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsWidth+$_SwitchKnobsLabelMargin
				$Y = $_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight/2-0.8*$_SwitchKnobsFontSize
				$_SwitchKnobs[$KnobNo][18] = GUICtrlCreateLabel($Right,$X,$Y,$_SwitchKnobsLabelWidth)
				If $_SwitchKnobsControlsFixed Then GUICtrlSetResizing($_SwitchKnobs[$KnobNo][18],$GUI_DOCKALL)
				GUICtrlSetBkColor($_SwitchKnobs[$KnobNo][18],$_SwitchKnobsTest ? $_SwitchKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_SwitchKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_SwitchKnobsFontColor)
				GUICtrlSetFont(-1,$_SwitchKnobsFontSize-1)
			EndIf
			If IsBool($Bottom) Then $Bottom = "Neutral"
			If StringLen($Bottom) > 0 Then
				$X = $_SwitchKnobs[$KnobNo][1]+$_SwitchKnobsWidth/2-$_SwitchKnobsTextWidth/2
				$Y = $_SwitchKnobs[$KnobNo][2]+$_SwitchKnobsHeight+$_SwitchKnobsLabelMargin-0.2*$_SwitchKnobsFontSize
				$_SwitchKnobs[$KnobNo][19] = GUICtrlCreateLabel($Bottom,$X,$Y,$_SwitchKnobsTextWidth,-1,$SS_CENTER)
				If $_SwitchKnobsControlsFixed Then GUICtrlSetResizing($_SwitchKnobs[$KnobNo][19],$GUI_DOCKALL)
				GUICtrlSetBkColor($_SwitchKnobs[$KnobNo][19],$_SwitchKnobsTest ? $_SwitchKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_SwitchKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_SwitchKnobsFontColor)
				GUICtrlSetFont(-1,$_SwitchKnobsFontSize-1)
			EndIf
			_SwitchKnobReplaceText($KnobNo)		; insert switch string in labels
			Return True
		EndIf
	Next
	Return False
EndFunc

Func _SwitchKnobReplaceText($KnobNo)	; internal: insert values in text
	If $_SwitchKnobs[$KnobNo][12] Then GUICtrlSetData($_SwitchKnobs[$KnobNo][11],StringReplace(StringReplace($_SwitchKnobs[$KnobNo][13],$_SwitchKnobsTextValue,SwitchKnobGetString($_SwitchKnobs[$KnobNo][0])),$_SwitchKnobsTextDefaultValue,SwitchKnobGetString($_SwitchKnobs[$KnobNo][0],$_SwitchKnobs[$KnobNo][8])))
	If $_SwitchKnobs[$KnobNo][15] Then _GUIToolTip_UpdateTipText($_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][6],0,GUICtrlGetHandle($_SwitchKnobs[$KnobNo][14]),StringReplace(StringReplace($_SwitchKnobs[$KnobNo][16],$_SwitchKnobsTextValue,SwitchKnobGetString($_SwitchKnobs[$KnobNo][0])),$_SwitchKnobsTextDefaultValue,SwitchKnobGetString($_SwitchKnobs[$KnobNo][0],$_SwitchKnobs[$KnobNo][8])))
EndFunc

; Enable or disable switch knob
; SwitchKnobEnable
;	$Knob							Knob handle
;	$Enable = True					Enable or disable
; )
; Return values
;	True or False					Enabled or disabled
;	False							If knob doesn't exist
Func SwitchKnobEnable($Knob,$Enable = True)
	If Not IsArray($_SwitchKnobs) Then Return False
	If Not $_SwitchKnobsEnabled Then Return True

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			If ($_SwitchKnobs[$KnobNo][20] And $Enable) Or (Not $_SwitchKnobs[$KnobNo][20] And Not $Enable) Then ExitLoop
			$_SwitchKnobs[$KnobNo][20] = $Enable
			If $Enable Then
				If $_SwitchKnobs[$KnobNo][12] Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_SwitchKnobs[$KnobNo][11],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][11],$_SwitchKnobsFontColor)
					EndIf
				EndIf
				GUICtrlSetState($_SwitchKnobs[$KnobNo][14],$GUI_ENABLE)
				If $_SwitchKnobs[$KnobNo][17] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_SwitchKnobs[$KnobNo][17],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][17],$_SwitchKnobsFontColor)
					EndIf
				EndIf
				If $_SwitchKnobs[$KnobNo][18] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_SwitchKnobs[$KnobNo][18],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][18],$_SwitchKnobsFontColor)
					EndIf
				EndIf
				If $_SwitchKnobs[$KnobNo][19] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_SwitchKnobs[$KnobNo][19],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][19],$_SwitchKnobsFontColor)
					EndIf
				EndIf
			Else
				If $_SwitchKnobs[$KnobNo][12] Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						If $_SwitchKnobsLabelsDisabled Then GUICtrlSetState($_SwitchKnobs[$KnobNo][11],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][11],$_SwitchKnobsFontDisabledColor)
					EndIf
				EndIf
				GUICtrlSetState($_SwitchKnobs[$KnobNo][14],$GUI_DISABLE)
				If $_SwitchKnobs[$KnobNo][17] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						If $_SwitchKnobsLabelsDisabled Then GUICtrlSetState($_SwitchKnobs[$KnobNo][17],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][17],$_SwitchKnobsFontDisabledColor)
					EndIf
				EndIf
				If $_SwitchKnobs[$KnobNo][18] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						If $_SwitchKnobsLabelsDisabled Then GUICtrlSetState($_SwitchKnobs[$KnobNo][18],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][18],$_SwitchKnobsFontDisabledColor)
					EndIf
				EndIf
				If $_SwitchKnobs[$KnobNo][19] <> -1 Then
					If $_SwitchKnobsFontColor = -1 Or $_SwitchKnobsFontDisabledColor = -1 Then
						If $_SwitchKnobsLabelsDisabled Then GUICtrlSetState($_SwitchKnobs[$KnobNo][19],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_SwitchKnobs[$KnobNo][19],$_SwitchKnobsFontDisabledColor)
					EndIf
				EndIf
			EndIf
			_SwitchKnobDraw($KnobNo)
			Return $Enable
		EndIf
	Next
	Return False
EndFunc

; Get if switch knob is enabled or disabled
; SwitchKnobEnabled(
;	$Knob							Knob handle
; )
; Return values
;	True or False					Enabled or disabled
;	False							If knob doesn't exist
Func SwitchKnobEnabled($Knob)
	If Not IsArray($_SwitchKnobs) Then Return False
	If Not $_SwitchKnobsEnabled Then Return True

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			Return $_SwitchKnobs[$KnobNo][20]
		EndIf
	Next
	Return False
EndFunc

; Show or hide switch knob
; SwitchKnobShow
;	$Knob							Knob handle
;	$Show = True					Show or hide
; )
; Return values
;	True or False					Shown or hidden
;	False							If knob doesn't exist
Func SwitchKnobShow($Knob,$Show = True)
	If Not IsArray($_SwitchKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			$_SwitchKnobs[$KnobNo][21] = $Show
			If $Show Then
				If $_SwitchKnobs[$KnobNo][12] Then GUICtrlSetState($_SwitchKnobs[$KnobNo][11],$GUI_SHOW)
				GUICtrlSetState($_SwitchKnobs[$KnobNo][14],$GUI_SHOW)
				If $_SwitchKnobs[$KnobNo][17] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][17],$GUI_SHOW)
				If $_SwitchKnobs[$KnobNo][18] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][18],$GUI_SHOW)
				If $_SwitchKnobs[$KnobNo][19] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][19],$GUI_SHOW)
			Else
				If $_SwitchKnobs[$KnobNo][12] Then GUICtrlSetState($_SwitchKnobs[$KnobNo][11],$GUI_HIDE)
				GUICtrlSetState($_SwitchKnobs[$KnobNo][14],$GUI_HIDE)
				If $_SwitchKnobs[$KnobNo][17] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][17],$GUI_HIDE)
				If $_SwitchKnobs[$KnobNo][18] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][18],$GUI_HIDE)
				If $_SwitchKnobs[$KnobNo][19] <> -1 Then GUICtrlSetState($_SwitchKnobs[$KnobNo][19],$GUI_HIDE)
			EndIf
			_SwitchKnobDraw($KnobNo)
			Return $Show
		EndIf
	Next
	Return False
EndFunc

; Draw a switch knob
; SwitchKnobDraw(
;	$Knob							Knob handle
; )
Func SwitchKnobDraw($Knob)
	If Not IsArray($_SwitchKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			_SwitchKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

Func _SwitchKnobDraw($KnobNo,$BrushClear = 0)			; internal: draw a switch
	Local $KnobGraphic = $_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][1]

	_GDIPlus_GraphicsResetTransform($KnobGraphic)
	If $_SwitchKnobs[$KnobNo][22] Then
		$_SwitchKnobs[$KnobNo][22] = False				; indicate knob is erased
		If $_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][7] = 0 Then
			Local $DC = _WinAPI_GetDC($_SwitchKnobs[$KnobNo][24]),$BackColor = 0
			If $DC <> 0 Then
				$BackColor = _WinAPI_GetBkColor($DC)
				_WinAPI_ReleaseDC($_SwitchKnobs[$KnobNo][24],$DC)
				If $BackColor = -1 Then
					$BackColor = _WinAPI_GetSysColor(4)				; color probably same as background color of popup menu $COLOR_MENU ($COLOR_WINDOW gives wrong color)
					If $BackColor = 0 Then $BackColor = 15790320	; fail save in case black is returned
				EndIf
			EndIf
			$_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][7] = _GDIPlus_BrushCreateSolid(0xFF000000+$BackColor)
		EndIf
		_GDIPlus_GraphicsFillRect($KnobGraphic,$_SwitchKnobs[$KnobNo][1]-2,$_SwitchKnobs[$KnobNo][2]-2,$_SwitchKnobsWidth+6,$_SwitchKnobsHeight+4,$_SwitchKnobsGUI[$_SwitchKnobs[$KnobNo][23]][7])
	EndIf
	If Not $_SwitchKnobs[$KnobNo][21] Then Return		; switch knob is hidden
	$_SwitchKnobs[$KnobNo][22] = True					; indicate knob is drawn

	If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
		_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsBack,$_SwitchKnobs[$KnobNo][1],$_SwitchKnobs[$KnobNo][2])	; draw back
	Else
		_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsBackDisabled,$_SwitchKnobs[$KnobNo][1],$_SwitchKnobs[$KnobNo][2])	; draw disabled back
	EndIf
	; draw front
	Switch $_SwitchKnobs[$KnobNo][7]
		Case 1
			If $_SwitchKnobs[$KnobNo][10] Then
				If $_SwitchKnobs[$KnobNo][9] Then
					If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOn,$_SwitchKnobs[$KnobNo][5],$_SwitchKnobs[$KnobNo][6])
					Else
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOnDisabled,$_SwitchKnobs[$KnobNo][5],$_SwitchKnobs[$KnobNo][6])
					EndIf
				Else
					If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFront,$_SwitchKnobs[$KnobNo][5],$_SwitchKnobs[$KnobNo][6])
					Else
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontDisabled,$_SwitchKnobs[$KnobNo][5],$_SwitchKnobs[$KnobNo][6])
					EndIf
				EndIf
			Else
				If $_SwitchKnobs[$KnobNo][9] Then
					If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOn,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
					Else
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOnDisabled,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
					EndIf
				Else
					If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFront,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
					Else
						_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontDisabled,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
					EndIf
				EndIf
			EndIf
		Case 0
			If $_SwitchKnobs[$KnobNo][9] Then
				If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOff,$_SwitchKnobs[$KnobNo][4],$_SwitchKnobs[$KnobNo][6])
				Else
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOffDisabled,$_SwitchKnobs[$KnobNo][4],$_SwitchKnobs[$KnobNo][6])
				EndIf
			Else
				If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFront,$_SwitchKnobs[$KnobNo][4],$_SwitchKnobs[$KnobNo][6])
				Else
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontDisabled,$_SwitchKnobs[$KnobNo][4],$_SwitchKnobs[$KnobNo][6])
				EndIf
			EndIf
		Case 2
			If $_SwitchKnobs[$KnobNo][9] Then
				If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOn,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
				Else
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontOnDisabled,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
				EndIf
			Else
				If Not $_SwitchKnobsEnabled Or $_SwitchKnobs[$KnobNo][20] Then
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFront,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
				Else
					_GDIPlus_GraphicsDrawImage($KnobGraphic,$_SwitchKnobsFrontDisabled,$_SwitchKnobs[$KnobNo][3],$_SwitchKnobs[$KnobNo][6])
				EndIf
			EndIf
	EndSwitch
EndFunc

; Flip a switch
; SwitchKnobFlip(
;	$Knob							Knob handle
; )
; Return values
;	True							Switch flipped
;	False							If knob doesn't exist
Func SwitchKnobFlip($Knob)
	If Not IsArray($_SwitchKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			_SwitchKnobFlip($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

Func _SwitchKnobFlip($KnobNo)			; internal: flip a switch
	Switch $_SwitchKnobs[$KnobNo][7]
		Case 0
			$_SwitchKnobs[$KnobNo][7] = 1
		Case 1
			If $_SwitchKnobs[$KnobNo][10] Then
				$_SwitchKnobs[$KnobNo][7] = 2
			Else
				$_SwitchKnobs[$KnobNo][7] = 0
			EndIf
		Case 2
			$_SwitchKnobs[$KnobNo][7] = 0
	EndSwitch
	_SwitchKnobReplaceText($KnobNo)
	_SwitchKnobDraw($KnobNo)
EndFunc

; Reset switch knob to default value
; SwitchKnobReset(
;	$Knob							Knob handle
; )
; Return values
;	True							Switch knob resetted
;	False							If knob doesn't exist
Func SwitchKnobReset($Knob)
	If Not IsArray($_SwitchKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			$_SwitchKnobs[$KnobNo][7] = $_SwitchKnobs[$KnobNo][8]
			_SwitchKnobReplaceText($KnobNo)
			_SwitchKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Set switch knob value
; SwitchKnobSetValue(
;	$Knob							Knob handle
;	$Value							Knob value
; )
; Return values
;	True							Switch knob set
;	False							If knob doesn't exist
Func SwitchKnobSetValue($Knob,$Value)
	If Not IsArray($_SwitchKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			If $Value < 0 Then
				$Value = 0
			ElseIf $_SwitchKnobs[$KnobNo][10] Then
				If $Value > 2 Then $Value = 2
			ElseIf $Value > 1 Then
				$Value = 1
			EndIf
			$_SwitchKnobs[$KnobNo][7] = $Value
			_SwitchKnobReplaceText($KnobNo)
			_SwitchKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Get switch knob value
; SwitchKnobGetValue(
;	$Knob							Knob handle
; )
; Return values
;	Knob value						Value of switch knob (0, 1, 2)
;	0								If knob doesn't exist
Func SwitchKnobGetValue($Knob)			;  switch value
	If Not IsArray($_SwitchKnobs) Then Return 0

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then Return $_SwitchKnobs[$KnobNo][7]
	Next
	Return 0
EndFunc

; Get switch knob default value
; SwitchKnobGetDefaultValue(
;	$Knob							Knob handle
; )
; Return values
;	Knob value						Default value of switch knob (0, 1, 2)
;	0								If knob doesn't exist
Func SwitchKnobGetDefaultValue($Knob)
	If Not IsArray($_SwitchKnobs) Then Return 0

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then Return $_SwitchKnobs[$KnobNo][8]
	Next
	Return 0
EndFunc

; Get switch knob value or label
; SwitchKnobGetString(
;	$Knob							Knob handle
;	$Value							Value to return, -1 = switch knob value (0, 1, 2), 0 = left label, 1 = right label, 2 = bottom label
; )
; Return value
; Value or label					Switch knob value or left/right/bottom label
; Empty string						If knob doesn't exist
Func SwitchKnobGetString($Knob,$Value = -1)
	If Not IsArray($_SwitchKnobs) Then Return ""

	For $KnobNo = 0 To Ubound($_SwitchKnobs)-1
		If $_SwitchKnobs[$KnobNo][0] = $Knob Then
			If $Value = -1 Then $Value = $_SwitchKnobs[$KnobNo][7]
			Switch $Value
				Case 0
					If $_SwitchKnobs[$KnobNo][17] <> -1 Then Return GUICtrlRead($_SwitchKnobs[$KnobNo][17])
				Case 1
					If $_SwitchKnobs[$KnobNo][10] Then
						If $_SwitchKnobs[$KnobNo][19] <> -1 Then Return GUICtrlRead($_SwitchKnobs[$KnobNo][19])
					Else
						If $_SwitchKnobs[$KnobNo][18] <> -1 Then Return GUICtrlRead($_SwitchKnobs[$KnobNo][18])
					EndIf
				Case 2
					If $_SwitchKnobs[$KnobNo][18] <> -1 Then Return GUICtrlRead($_SwitchKnobs[$KnobNo][18])
			EndSwitch
		EndIf
	Next
	Return ""
EndFunc

; Don't use, for backwards compatibility only
Func SwitchKnobGetGUI($GUIno = 0)
	Return $_SwitchKnobsGUI[$GUIno][0]
EndFunc

; Initialize the use of rotary knobs
; RotaryKnobsInitialize(
;	$GUI							Handle of GUI
;	$FileDialBack					File name of rotary knob background image
;	$FileDialFront					File name of rotary knob dial foreground image
;	$FileFront						File name of rotary knob foreground image
;	$ImagesDirectory = @ScriptDir	Directory of image files
;	$FontSize = 8.5					Font size of GUI
;	$FontColor = -1					Font color, default is GUI font color
;	$TextWidth = 140				Text width of rotary knob
;	$LabelWidth = 60				Label width of rotary knob examples: "on", "off"
;	$LabelRadius = 6				Margin between labels and background image
;	$ControlsFixed = True			Controls are fixed on their positions or repositioned at a window size change
; )
; Return values
;	0								Successful
;	-1								Images directory doesn't exist
;	-2								Knob background image file doesn't exist
;	-3								Knob foreground image file doesn't exist
;	-4								Knob dial image file doesn't exist
Func RotaryKnobsInitialize($GUI,$FileDialBack,$FileDialFront,$FileFront,$ImagesDirectory = @ScriptDir,$FontSize = 8.5,$FontColor = -1,$TextWidth = 140,$LabelWidth = 60,$LabelRadius = 6,$ControlsFixed = True)
	Global $_RotaryKnobs,$_RotaryKnobsHandle,$_RotaryKnobsAnimation = 0,$_RotaryKnobsAniFrames = [-1,50,-1,-1]
	Global $_RotaryKnobsDir,$_RotaryKnobsDialBack,$_RotaryKnobsDialFront,$_RotaryKnobsFront,$_RotaryKnobsDialBackDisabled = 0,$_RotaryKnobsDialFrontDisabled = 0,$_RotaryKnobsFrontDisabled = 0,$_RotaryKnobs0Knobs = False,$_RotaryKnobsDialBack0 = 0,$_RotaryKnobsDialFront0 = 0,$_RotaryKnobsFront0 = 0
	Global $_RotaryKnobsEnableOn,$_RotaryKnobsOn = -1,$_RotaryKnobsOnX = 0,$_RotaryKnobsOnY = 0,$_RotaryKnobsOnFrames = [-1,50,0,-1],$_RotaryKnobsOffFrames = [-1,50,0,-1],$_RotaryKnobsEnableOff,$_RotaryKnobsOff = -1,$_RotaryKnobsOffX= 0,$_RotaryKnobsOffY = 0,$_RotaryKnobsScanSize
	Global $_RotaryKnobsFontSize,$_RotaryKnobsFontColor,$_RotaryKnobsLabelRadius,$_RotaryKnobsControlsFixed,$_RotaryKnobsTextWidth,$_RotaryKnobsLabelWidth,$_RotaryKnobsWidth,$_RotaryKnobsHeight,$_RotaryKnob
	Global $_RotaryKnobsEnabled,$_RotaryKnobsLabelsDisabled,$_RotaryKnobsFontDisabledColor,$_RotaryKnobsTest,$_RotaryKnobsTestColor,$_RotaryKnobsTextValue,$_RotaryKnobsTextLowValue,$_RotaryKnobsTextHighValue,$_RotaryKnobsTextDefaultValue,$_RotaryKnobsTextSnapValue,$_RotaryKnobsTextConversionValue

	_RotaryKnobsAddGUI($GUI,True)
	$_RotaryKnobs = False
	$_RotaryKnobsHandle = 0
	$_RotaryKnobsDir = $ImagesDirectory
	$_RotaryKnobsDialBack = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialBack)
	$_RotaryKnobsWidth = _GDIPlus_ImageGetWidth($_RotaryKnobsDialBack)
	$_RotaryKnobsHeight = _GDIPlus_ImageGetHeight($_RotaryKnobsDialBack)
	$_RotaryKnobsDialFront = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialFront)
	$_RotaryKnobsFront = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileFront)
	$_RotaryKnobsFontSize = $FontSize
	$_RotaryKnobsFontColor = $FontColor
	$_RotaryKnobsTextWidth = Round($TextWidth*$FontSize/8.5)
	$_RotaryKnobsLabelWidth = Round($LabelWidth*$FontSize/8.5)
	$_RotaryKnobsLabelRadius = $LabelRadius
	$_RotaryKnobsControlsFixed = $ControlsFixed
	$_RotaryKnob = -1		; current knob being dialed
	$_RotaryKnobsTextValue = "%v"
	$_RotaryKnobsTextLowValue = "%l"
	$_RotaryKnobsTextHighValue = "%h"
	$_RotaryKnobsTextDefaultValue = "%d"
	$_RotaryKnobsTextSnapValue = "%s"
	$_RotaryKnobsTextConversionValue = "%c"
	$_RotaryKnobsEnabled = False
	$_RotaryKnobsLabelsDisabled = True
	$_RotaryKnobsFontDisabledColor = -1
	$_RotaryKnobsEnableOn = False
	$_RotaryKnobsEnableOff = False
	$_RotaryKnobsScanSize = 1
	$_RotaryKnobsTest = False
	$_RotaryKnobsTestColor = 0xFFFFFF
	If Not FileExists($ImagesDirectory) Then Return -1
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialBack) Then Return -2
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialFront) Then Return -3
	If Not FileExists($_RotaryKnobsDir & "\" & $FileFront) Then Return -4
	Return 0
EndFunc

; Add another GUI for rotary knobs
; RotaryKnobsAddGUI(
;	$GUI							Handle of GUI
;	$InitialTime = 500 ms			Initial delay
;	$AutoPopTime = 5000 ms			Time of showing
;	$ReshowTime = 100 ms			show delay between controls
; )
Func RotaryKnobsAddGUI($GUI,$InitialTime = 500,$AutoPopTime = 5000, $ReshowTime = 100)
	_RotaryKnobsAddGUI($GUI,False,$InitialTime,$AutoPopTime,$ReshowTime)
EndFunc

Func _RotaryKnobsAddGUI($GUI,$First = False,$InitialTime = 500,$AutoPopTime = 5000,$ReshowTime = 100)	; Internal function to add a GUI for rotary knobs
	Local $GUIPosition,$GUIno

	If $First Then
		Global $_RotaryKnobsGUI[1][8]
	Else
		ReDim $_RotaryKnobsGUI[UBound($_RotaryKnobsGUI)+1][8]
	EndIf
	$GUIno = UBound($_RotaryKnobsGUI)-1
	$_RotaryKnobsGUI[$GUIno][0] = $GUI
	_RotaryKnobsCreateGraphicObject($GUIno)
	$GUIPosition = WinGetPos($GUI)
	; correct for minimize state
	If $GUIPosition[0] = -32000 Then $GUIPosition[0] = 0
	If $GUIPosition[1] = -32000 Then $GUIPosition[1] = 0
	$_RotaryKnobsGUI[$GUIno][2] = $GUIPosition[0]
	$_RotaryKnobsGUI[$GUIno][3] = $GUIPosition[1]
	$_RotaryKnobsGUI[$GUIno][4] = $GUIPosition[2]
	$_RotaryKnobsGUI[$GUIno][5] = $GUIPosition[3]
	$_RotaryKnobsGUI[$GUIno][6] = _GUIToolTip_Create($GUI)
	_GUIToolTip_SetMaxTipWidth($_RotaryKnobsGUI[$GUIno][6],1000)
	If $InitialTime = -1 Then
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_AUTOMATIC,-1)
	Else
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_INITIAL,$InitialTime)
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_AUTOPOP,$AutoPopTime)
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_RESHOW,$ReshowTime)
	EndIf
	$_RotaryKnobsGUI[$GUIno][7] = 0	; clear brush
EndFunc

; Set times of tooltips
; RotaryKnobsToolTipsTimes(
;	$GUIno							Number of GUI, 0 = first
;	$InitialTime = 500				Initial delay for showing
;	$AutoPopTime = 5000				Total time of showing
;	$ReshowTime = 100				Delay to next control
; )
Func RotaryKnobsToolTipsTimes($GUIno,$InitialTime = 500,$AutoPopTime = 5000, $ReshowTime = 100)
	If $InitialTime = -1 Then
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_AUTOMATIC,-1)
	Else
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_INITIAL,$InitialTime)
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_AUTOPOP,$AutoPopTime)
		_GUIToolTip_SetDelayTime($_RotaryKnobsGUI[$GUIno][6],$TTDT_RESHOW,$ReshowTime)
	EndIf
EndFunc

; Get GUI handle by GUI number
; RotaryKnobsGetGUI(
;	$GUIno							Number of GUI, 0 = first
; )
Func RotaryKnobsGetGUI($GUIno = 0)
	Return $_RotaryKnobsGUI[$GUIno][0]
EndFunc

; Check if given GUI is a rotary knobs GUI
; RotaryKnobsIsGUI(
;	$GUI							Handle of GUI
; )
; Return values
;	True							Handle is a switch knobs GUI
;	False							Not
Func RotaryKnobsIsGUI($GUI)
	For $GUIno = 0 To UBound($_RotaryKnobsGUI)-1
		If $_RotaryKnobsGUI[$GUIno][0] = $GUI Then Return True
	Next
	Return False
EndFunc

; Get GDI plus graphic handle by GUI number
; RotaryKnobsGetGraphic(
;	$GUIno							Number of GUI, 0 = first
; )
Func RotaryKnobsGetGraphic($GUIno = 0)
	Return $_RotaryKnobsGUI[$GUIno][1]
EndFunc

Func _RotaryKnobsCreateGraphicObject($GUIno)	; Internal function to create graphics object
	$_RotaryKnobsGUI[$GUIno][1] = _GDIPlus_GraphicsCreateFromHWND($_RotaryKnobsGUI[$GUIno][0])
	_GDIPlus_GraphicsSetInterpolationMode($_RotaryKnobsGUI[$GUIno][1],$GDIP_INTERPOLATIONMODE_HIGHQUALITYBICUBIC)
EndFunc

Func _RotaryKnobActiveWindow()				; Internal function to detect an active rotary knobs window
	If Not IsArray($_RotaryKnobs) Then Return -1
	For $GUIno = 0 To UBound($_RotaryKnobsGUI)-1
		If WinActive($_RotaryKnobsGUI[$GUIno][0]) Then Return $GUIno
	Next
	Return -1
EndFunc

; Initialize the rotary knobs for 0 knobs
; RotaryKnobs0Knobs(
; 	$FileDialBack					File name of disabled rotary knob background image
; 	$FileDialFront					File name of disabled rotary knob dial foreground image
; 	$FileFront						File name of disabled rotary knob foreground image
; )
; Return values
;	0								Images set
;	-1								Images directory doesn't exist
;	-2								Disabled knob background image file doesn't exist
;	-3								disabled knob foreground image file doesn't exist
;	-4								Disabled knob dial image file doesn't exist
Func RotaryKnobs0Knobs($FileDialBack,$FileDialFront,$FileFront)
	If Not FileExists($_RotaryKnobsDir) Then Return -1
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialBack) Then Return -2
	$_RotaryKnobsDialBack0 = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialBack)
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialFront) Then Return -3
	$_RotaryKnobsDialFront0 = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialFront)
	If Not FileExists($_RotaryKnobsDir & "\" & $FileFront) Then Return -4
	$_RotaryKnobsFront0 = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileFront)
	$_RotaryKnobs0Knobs = True
	Return 0
EndFunc

; Initialize the enable/disable rotary knobs feature
; RotaryKnobsEnableDisable(
; 	$FileDialBack					File name of disabled rotary knob background image
; 	$FileDialFront					File name of disabled rotary knob dial foreground image
; 	$FileFront						File name of disabled rotary knob foreground image
; 	$Enable = True
; )
; Return values
;	True or False					Enabled/disabled feature set to on or off
;	-1								Images directory doesn't exist
;	-2								Disabled knob background image file doesn't exist
;	-3								disabled knob foreground image file doesn't exist
;	-4								Disabled knob dial image file doesn't exist
Func RotaryKnobsEnableDisable($FileDialBack,$FileDialFront,$FileFront,$Enable = True)	; enable disabling of rotary knobs
	$_RotaryKnobsEnabled = $Enable
	If IsBool($FileDialBack) Then $_RotaryKnobsEnabled = $FileDialBack
	If Not FileExists($_RotaryKnobsDir) Then
		$_RotaryKnobsEnabled = False
		Return -1
	EndIf
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialBack) Then
		$_RotaryKnobsEnabled = False
		Return -2
	EndIf
	$_RotaryKnobsDialBackDisabled = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialBack)
	If Not FileExists($_RotaryKnobsDir & "\" & $FileDialFront) Then
		$_RotaryKnobsEnabled = False
		Return -3
	EndIf
	$_RotaryKnobsDialFrontDisabled = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileDialFront)
	If Not FileExists($_RotaryKnobsDir & "\" & $FileFront) Then
		$_RotaryKnobsEnabled = False
		Return -4
	EndIf
	$_RotaryKnobsFrontDisabled = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileFront)
	Return $_RotaryKnobsEnabled
EndFunc

; Set if rotary labels are shown as disabled
; RotaryKnobsLabelsDisabled(
;	$Disabled = True
; )
Func RotaryKnobsLabelsDisabled($Disabled = True)
	$_RotaryKnobsLabelsDisabled = $Disabled
EndFunc

; Set color of disabled rotary knobs (color for enabled knobs must be provided)
; RotaryKnobsLabelsDisabledColor(
;	$Color = -1						set -1 to reset
; )
Func RotaryKnobsLabelsDisabledColor($Color = -1)
	$_RotaryKnobsFontDisabledColor = $Color
EndFunc

; Set color of rotary knobs
; RotaryKnobsLabelsColor(
;	$Color = -1						set -1 to reset
; )
Func RotaryKnobsLabelsColor($Color = -1)
	$_RotaryKnobsFontColor = $Color
EndFunc

; Set file images to show "on" by other value than default value and "off" by user switch off
; RotaryKnobsOnOff(
;	$FileOn							File of "on"-image
;	$FileOff = ""					File of "off"-image, default no image
; )
Func RotaryKnobsOnOff($FileOn,$FileOff = "")
	$_RotaryKnobsEnableOn = True
	If IsArray($FileOn) Then
		Global $_RotaryKnobsOn[UBound($FileOn)][3]
		For $File = 0 To UBound($FileOn)-1
			$_RotaryKnobsOn[$File][0] = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileOn[$File])
			$_RotaryKnobsOn[$File][1] = -_GDIPlus_ImageGetWidth($_RotaryKnobsOn[$File][0])/2
			$_RotaryKnobsOn[$File][2] = -_GDIPlus_ImageGetHeight($_RotaryKnobsOn[$File][0])/2
		Next
	Else
		$_RotaryKnobsOn = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileOn)
		$_RotaryKnobsOnX = -_GDIPlus_ImageGetWidth($_RotaryKnobsOn)/2
		$_RotaryKnobsOnY = -_GDIPlus_ImageGetHeight($_RotaryKnobsOn)/2
	EndIf
	If IsArray($FileOff) Then
		$_RotaryKnobsEnableOff = True
		Global $_RotaryKnobsOff[UBound($FileOff)][3]
		For $File = 0 To UBound($FileOff)-1
			$_RotaryKnobsOff[$File][0] = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileOff[$File])
			$_RotaryKnobsOff[$File][1] = -_GDIPlus_ImageGetWidth($_RotaryKnobsOff[$File][0])/2
			$_RotaryKnobsOff[$File][2] = -_GDIPlus_ImageGetHeight($_RotaryKnobsOff[$File][0])/2
		Next
		ElseIf StringLen($FileOff) > 0 Then
		$_RotaryKnobsEnableOff = True
		$_RotaryKnobsOff = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileOff)
		$_RotaryKnobsOffX = -_GDIPlus_ImageGetWidth($_RotaryKnobsOff)/2
		$_RotaryKnobsOffY = -_GDIPlus_ImageGetHeight($_RotaryKnobsOff)/2
	EndIf
EndFunc

; Started when knob isn't its default value
Func _RotaryKnobsOnStart($KnobNo)
	If Not $_RotaryKnobsEnableOn Or Not IsArray($_RotaryKnobsOn) Then Return False

	$_RotaryKnobsOnFrames[0] = $KnobNo
	$_RotaryKnobsOnFrames[2] = UBound($_RotaryKnobsOn)-1	; frame no
	$_RotaryKnobsOnFrames[3] = UBound($_RotaryKnobsOn)		; last frame drawn
	AdlibRegister("_RotaryKnobsOnAnimate",$_RotaryKnobsOnFrames[1])
	Return True
EndFunc

; Started when knob is disabled
Func _RotaryKnobsOffStart($KnobNo)
	If Not $_RotaryKnobsEnableOff Or Not IsArray($_RotaryKnobsOff) Then Return False

	$_RotaryKnobsOffFrames[0] = $KnobNo
	$_RotaryKnobsOffFrames[2] = UBound($_RotaryKnobsOff)-1	; frame no
	$_RotaryKnobsOffFrames[3] = UBound($_RotaryKnobsOff)	; last frame drawn
	AdlibRegister("_RotaryKnobsOffAnimate",$_RotaryKnobsOffFrames[1])
	Return True
EndFunc

Func _RotaryKnobsOnAnimate()
	AdlibUnRegister("_RotaryKnobsOnAnimate")
	$_RotaryKnobsOnFrames[2] -= 1
	If $_RotaryKnobsOnFrames[2] <= 0 Then Return
	AdlibRegister("_RotaryKnobsOnAnimate",$_RotaryKnobsOnFrames[1])
EndFunc

Func _RotaryKnobsOffAnimate()
	AdlibUnRegister("_RotaryKnobsOffAnimate")
	$_RotaryKnobsOffFrames[2] -= 1
	If $_RotaryKnobsOffFrames[2] <= 0 Then Return
	AdlibRegister("_RotaryKnobsOffAnimate",$_RotaryKnobsOffFrames[1])
EndFunc

Func RotaryKnobsAnimate($FileImages)
	If Not IsArray($FileImages) Then Return -1
	Global $_RotaryKnobsAnimation[UBound($FileImages)][3]
	For $File = 0 To UBound($FileImages)-1
		$_RotaryKnobsAnimation[$File][0] = _GDIPlus_ImageLoadFromFile($_RotaryKnobsDir & "\" & $FileImages[$File])
		$_RotaryKnobsAnimation[$File][1] = -_GDIPlus_ImageGetWidth($_RotaryKnobsAnimation[$File][0])/2
		$_RotaryKnobsAnimation[$File][2] = -_GDIPlus_ImageGetHeight($_RotaryKnobsAnimation[$File][0])/2
	Next
	Return 0
EndFunc

; Started when knob dailing is done
Func _RotaryKnobsAnimateStart($KnobNo)
	If Not IsArray($_RotaryKnobsAnimation) Then Return False

	If $_RotaryKnobsAniFrames[0] > -1 And $_RotaryKnobsAniFrames[2] > -1 Then
		$_RotaryKnobsAniFrames[2] = -1
		RotaryKnobDrawFrame()	; reset previous knob during its animation
	EndIf
	$_RotaryKnobsAniFrames[0] = $KnobNo
	$_RotaryKnobsAniFrames[2] = UBound($_RotaryKnobsAnimation)-1	; frame no
	$_RotaryKnobsAniFrames[3] = UBound($_RotaryKnobsAnimation)		; last frame drawn
	AdlibRegister("_RotaryKnobsAnimate",$_RotaryKnobsAniFrames[1])
	Return True
EndFunc

Func _RotaryKnobsAnimate()
	AdlibUnRegister("_RotaryKnobsAnimate")
	$_RotaryKnobsAniFrames[2] -= 1
	If $_RotaryKnobsAniFrames[2] < 0 Then Return
	AdlibRegister("_RotaryKnobsAnimate",$_RotaryKnobsAniFrames[1])
EndFunc

; Draw next frame of an animation
; RotaryKnobDrawFrame()
; Remark: having animations on the AutoIt only thread will affect the user interaction
Func RotaryKnobDrawFrame()
	If $_RotaryKnobsAniFrames[0] > -1 And $_RotaryKnobsAniFrames[2] < $_RotaryKnobsAniFrames[3] Then
		_RotaryKnobDraw($_RotaryKnobsAniFrames[0])	; only draw frame if it wasn't drawn already
		$_RotaryKnobsAniFrames[3] = $_RotaryKnobsAniFrames[2]
		Return
	EndIf
	If $_RotaryKnobsOnFrames[0] > -1 And $_RotaryKnobsOnFrames[2] < $_RotaryKnobsOnFrames[3] Then
		_RotaryKnobDraw($_RotaryKnobsOnFrames[0])	; only draw frame if it wasn't drawn already
		$_RotaryKnobsOnFrames[3] = $_RotaryKnobsOnFrames[2]
		Return
	EndIf
	If $_RotaryKnobsOffFrames[0] > -1 And $_RotaryKnobsOffFrames[2] < $_RotaryKnobsOffFrames[3] Then
		_RotaryKnobDraw($_RotaryKnobsOffFrames[0])	; only draw frame if it wasn't drawn already
		$_RotaryKnobsOffFrames[3] = $_RotaryKnobsOffFrames[2]
		Return
	EndIf
EndFunc

; Set the scanning knob surface size
; RotaryKnobsScanSize(
;	$SizeFactor = 1					Scan surface width/height is multiplied by this factor
; )
; Return Value
;	Previous size factor
Func RotaryKnobsScanSize($SizeFactor = 1)
	Local $Factor = $_RotaryKnobsScanSize
	$_RotaryKnobsScanSize = $SizeFactor
	Return $Factor
EndFunc

; Test rotary knobs (showing label lengths by different background color)
; RotaryKnobsTest(
;	$Test = True					Set testing on or off
;	$Color = 0xFFFFFF				set color of labels
; )
Func RotaryKnobsTest($Test = True,$Color = 0xFFFFFF)
	$_RotaryKnobsTest = $Test
	$_RotaryKnobsTestColor = $Color
EndFunc

; Clean up GDI objects
Func RotaryKnobsDestroy()
	If Not IsArray($_RotaryKnobs) Then Return False

	For $GUIno = 0 To UBound($_RotaryKnobsGUI)-1
		_GDIPlus_GraphicsDispose($_RotaryKnobsGUI[$GUIno][1])
		If $_RotaryKnobsGUI[$GUIno][7] <> 0 Then _GDIPlus_BrushDispose($_RotaryKnobsGUI[$GUIno][7])
	Next
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		_GDIPlus_GraphicsDispose($_RotaryKnobs[$KnobNo][6])
		_GDIPlus_BitmapDispose($_RotaryKnobs[$KnobNo][5])
	Next
	_GDIPlus_ImageDispose($_RotaryKnobsDialBack)
	_GDIPlus_ImageDispose($_RotaryKnobsDialFront)
	_GDIPlus_ImageDispose($_RotaryKnobsFront)
	If IsArray($_RotaryKnobsAnimation) Then
		For $Image = 0 To UBound($_RotaryKnobsAnimation)-1
			_GDIPlus_ImageDispose($_RotaryKnobsAnimation[$Image][0])
		Next
	EndIf
	If $_RotaryKnobsDialBack0 <> 0 Then _GDIPlus_ImageDispose($_RotaryKnobsDialBack0)
	If $_RotaryKnobsDialFront0 <> 0 Then _GDIPlus_ImageDispose($_RotaryKnobsDialFront0)
	If $_RotaryKnobsFront0 <> 0 Then _GDIPlus_ImageDispose($_RotaryKnobsFront0)
	If $_RotaryKnobsEnabled Then
		_GDIPlus_ImageDispose($_RotaryKnobsDialBackDisabled)
		_GDIPlus_ImageDispose($_RotaryKnobsDialFrontDisabled)
		_GDIPlus_ImageDispose($_RotaryKnobsFrontDisabled)
	EndIf
	If $_RotaryKnobsEnableOn Then
		If IsArray($_RotaryKnobsOn) Then
			For $Image = 0 To UBound($_RotaryKnobsOn)-1
				_GDIPlus_ImageDispose($_RotaryKnobsOn[$Image][0])
			Next
		Else
			_GDIPlus_ImageDispose($_RotaryKnobsOn)
		EndIf
	EndIf
	If $_RotaryKnobsEnableOff Then
		If IsArray($_RotaryKnobsOff) Then
			For $Image = 0 To UBound($_RotaryKnobsOff)-1
				_GDIPlus_ImageDispose($_RotaryKnobsOff[$Image][0])
			Next
		Else
			_GDIPlus_ImageDispose($_RotaryKnobsOff)
		EndIf
	EndIf
	Return True
EndFunc

; Scan and register rotary knob user is hovering above (if any)
; RotaryKnobsScan(
;	$HoverOnly = False				Hover only, don't register
; )
; Return values
;	Knob handle						Knob user is hovering above
;	-1
Func RotaryKnobsScan($HoverOnly = False)
	Local $GUIno = _RotaryKnobActiveWindow()
	$_RotaryKnob = -1
	If $GUIno = -1 Then Return -1

	Local $aCursor
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][36] <> $GUIno Then ContinueLoop
		$aCursor = GUIGetCursorInfo($_RotaryKnobsGUI[$GUIno][0])
		If $aCursor[0] >= $_RotaryKnobs[$KnobNo][1]+(0.5-0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsWidth And $aCursor[0] <= $_RotaryKnobs[$KnobNo][1]+(0.5+0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsWidth And $aCursor[1] >= $_RotaryKnobs[$KnobNo][2]+(0.5-0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsHeight And $aCursor[1] <= $_RotaryKnobs[$KnobNo][2]+(0.5+0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsHeight Then
			$_RotaryKnobs[$KnobNo][17] = $aCursor[1]
			If Not $HoverOnly And (Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32]) And (Not $_RotaryKnobsEnableOff Or Not $_RotaryKnobs[$KnobNo][35]) Then $_RotaryKnob = $KnobNo	; let the knob be dialed
			Return $_RotaryKnobs[$KnobNo][0]
		EndIf
	Next
	Return -1
EndFunc

; Force redraw of all rotary knobs
; RotaryKnobsRedraw(
; 	$aMsg							GUI message array
; )
; Return value
;	True							Redrawn
;	False							No redraw was needed
Func RotaryKnobsRedraw($aMsg)
	Local $GUI = -1,$Graphic
	For $GUIno = 0 To UBound($_RotaryKnobsGUI)-1
		If $aMsg[1] = $_RotaryKnobsGUI[$GUIno][0] Then
			$GUI = $GUIno
			ExitLoop
		EndIf
	Next
	If $GUI = -1 Then Return False
	If $aMsg[0] = $GUI_EVENT_RESTORE Or $aMsg[0] = $GUI_EVENT_MAXIMIZE Or $aMsg[0] = $GUI_EVENT_RESIZED Then
		If $aMsg[0] = $GUI_EVENT_MAXIMIZE Or $aMsg[0] = $GUI_EVENT_RESIZED Then	; create new graphic object because window has been resized
			_GDIPlus_GraphicsDispose($_RotaryKnobsGUI[$GUI][1])
			_RotaryKnobsCreateGraphicObject($GUI)
		EndIf
		RotaryKnobsDraw($GUI)
		Return True
	ElseIf Not BitAND(WinGetState($_RotaryKnobsGUI[$GUI][0]),16) Then		; window isn't minimized so check
		Local $Position = WinGetPos($_RotaryKnobsGUI[$GUI][0])
		If $Position[0] <> $_RotaryKnobsGUI[$GUI][2] Or $Position[1] <> $_RotaryKnobsGUI[$GUI][3] Or $Position[2] <> $_RotaryKnobsGUI[$GUI][4] Or $Position[3] <> $_RotaryKnobsGUI[$GUI][5] Then
			$_RotaryKnobsGUI[$GUI][2] = $Position[0]
			$_RotaryKnobsGUI[$GUI][3] = $Position[1]
			If $Position[2] <> $_RotaryKnobsGUI[$GUI][4] Or $Position[3] <> $_RotaryKnobsGUI[$GUI][5] Then	; create new graphic object because window has been resized
				_GDIPlus_GraphicsDispose($_RotaryKnobsGUI[$GUI][1])
				_RotaryKnobsCreateGraphicObject($GUI)
			EndIf
			$_RotaryKnobsGUI[$GUI][4] = $Position[2]
			$_RotaryKnobsGUI[$GUI][5] = $Position[3]
			RotaryKnobsDraw($GUI)
			Return True
		EndIf
	EndIf
	Return False
EndFunc

; Reset all rotary knobs to default
; RotaryKnobsResetAll(
;	$GUIno							Number of GUI, reset -1 = all rotary knobs GUIs, 0 = first
; )
Func RotaryKnobsResetAll($GUIno = -1)
	If _RotaryKnobActiveWindow() = -1 Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $GUIno = -1 Or ($GUIno > -1 And $_RotaryKnobs[$KnobNo][36] = $GUIno) Then RotaryKnobReset($_RotaryKnobs[$KnobNo][0])
	Next
	Return True
EndFunc

; Reset rotary knob user hovering above to default value
; RotaryKnobsReset()
; Return values
;	Knob handle						Knob user is hovering above
;	-1								User isn't hovering above a knob
Func RotaryKnobsReset()
	Local $GUIno = _RotaryKnobActiveWindow()
	$_RotaryKnob = -1
	If $GUIno = -1 Then Return -1

	Local $aCursor
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][36] <> $GUIno Then ContinueLoop
		$aCursor = GUIGetCursorInfo($_RotaryKnobsGUI[$GUIno][0])
		If $aCursor[0] >= $_RotaryKnobs[$KnobNo][1]+(0.5-0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsWidth And $aCursor[0] <= $_RotaryKnobs[$KnobNo][1]+(0.5+0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsWidth And $aCursor[1] >= $_RotaryKnobs[$KnobNo][2]+(0.5-0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsHeight And $aCursor[1] <= $_RotaryKnobs[$KnobNo][2]+(0.5+0.5*$_RotaryKnobsScanSize)*$_RotaryKnobsHeight Then
			If Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32] Then RotaryKnobReset($_RotaryKnobs[$KnobNo][0])
			Return $_RotaryKnobs[$KnobNo][0]
		EndIf
	Next
	Return -1
EndFunc

; (Re)draw all rotary knobs
Func RotaryKnobsDraw($GUIno = 0)
	If Not IsArray($_RotaryKnobs) Then Return False

	Local $DC = _WinAPI_GetDC($_RotaryKnobsGUI[$GUIno][0]),$BackColor = 0
	If $DC <> 0 Then
		$BackColor = _WinAPI_GetBkColor($DC)
		_WinAPI_ReleaseDC($_RotaryKnobsGUI[$GUIno][0],$DC)
		If $BackColor = -1 Then
			$BackColor = _WinAPI_GetSysColor(4)				; color probably same as background color of popup menu $COLOR_MENU ($COLOR_WINDOW gives wrong color)
			If $BackColor = 0 Then $BackColor = 15790320	; fail save in case black is returned
		EndIf
	EndIf
	If $_RotaryKnobsGUI[$GUIno][7] <> 0 Then _GDIPlus_BrushDispose($_RotaryKnobsGUI[$GUIno][7])
	$_RotaryKnobsGUI[$GUIno][7] = _GDIPlus_BrushCreateSolid(0xFF000000+$BackColor)
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][36] <> $GUIno Then ContinueLoop
		_RotaryKnobDraw($KnobNo)
	Next
	Return True
EndFunc

; Create a rotary knob
; RotaryKnobCreate(
;	$X,$Y							Position of rotary knob
;	$Value = 0						Value of rotary knob
;	$LowValue = 0					Lowest value, when $SnapValue is an array this value will be set to 0
;	$HighValue = 100				Highest value, when $SnapValue is an array this value will be set to the highest index
;	$DefaultValue = 0				Default value, used for resetting rotary knob
;	$Resolution = 1					Resolution value per vertical pixel
;	$SnapValue = true				Value to snap to when dialing, default true = $Resolution
;									If $SnapValue is an array of values these values will be the base of the rotary knob
;	$StartAngle = -130				Rotary knob start angle, knob can be dailed to the left to this maximum value
;	$EndAngle = 130					Rotary knob end angle, knob can be dailed to the right to this maximum value
;	$ConversionRate					Conversion rate for showing in label with %c
;	$ConversionDecimals				Number of point decimals to show
; )
; Return value						Knob handle
Func RotaryKnobCreate($X,$Y,$Value = 0,$LowValue = 0,$HighValue = 100,$DefaultValue = 0,$Resolution = 1,$SnapValue = True,$StartAngle = -130,$EndAngle = 130,$Exponential = False,$ConversionRate = 1,$ConversionDecimals = 0)	; create a rotary knob
	Local $KnobNo

	If Not IsArray($_RotaryKnobs) Then
		$KnobNo = 0
		Global $_RotaryKnobs[1][42]
	Else
		$KnobNo = UBound($_RotaryKnobs)
		ReDim $_RotaryKnobs[$KnobNo+1][42]
	EndIf
	$_RotaryKnobsHandle += 1
	$_RotaryKnobs[$KnobNo][0] = $_RotaryKnobsHandle
	$_RotaryKnobs[$KnobNo][1] = $X
	$_RotaryKnobs[$KnobNo][2] = $Y
	$_RotaryKnobs[$KnobNo][3] = $X+$_RotaryKnobsWidth/2
	$_RotaryKnobs[$KnobNo][4] = $Y+$_RotaryKnobsHeight/2
	$_RotaryKnobs[$KnobNo][5] = _GDIPlus_BitmapCreateFromGraphics($_RotaryKnobsWidth+6,$_RotaryKnobsHeight+6,$_RotaryKnobsGUI[UBound($_RotaryKnobsGUI)-1][1])
	$_RotaryKnobs[$KnobNo][6] = _GDIPlus_ImageGetGraphicsContext($_RotaryKnobs[$KnobNo][5])
	_GDIPlus_GraphicsSetSmoothingMode($_RotaryKnobs[$KnobNo][6],$GDIP_SMOOTHINGMODE_HIGHQUALITY)
	$_RotaryKnobs[$KnobNo][7] = -_GDIPlus_ImageGetWidth($_RotaryKnobsDialFront)/2-0.5
	$_RotaryKnobs[$KnobNo][8] = -_GDIPlus_ImageGetHeight($_RotaryKnobsDialFront)/2-0.5
	If IsArray($SnapValue) Then
		$LowValue = 0
		$HighValue = UBound($SnapValue)-1
		$_RotaryKnobs[$KnobNo][9] = 0
		For $ValueNo = 0 To $HighValue
			If $Value >= $SnapValue[$ValueNo] Then	; take value if larger
				$_RotaryKnobs[$KnobNo][9] = $ValueNo
			Else
				ExitLoop
			EndIf
		Next
	Else
		If $Value > $HighValue Then
			$Value = $HighValue
		ElseIf $Value < $LowValue Then
			$Value = $LowValue
		EndIf
		$_RotaryKnobs[$KnobNo][9] = $Value
	EndIf
	$_RotaryKnobs[$KnobNo][10] = $LowValue
	$_RotaryKnobs[$KnobNo][11] = $HighValue
	If IsArray($SnapValue) Then
		$_RotaryKnobs[$KnobNo][12] = 0
		For $ValueNo = 0 To $HighValue
			If $DefaultValue >= $SnapValue[$ValueNo] Then	; take value if larger
				$_RotaryKnobs[$KnobNo][12] = $ValueNo
			Else
				ExitLoop
			EndIf
		Next
	Else
		$_RotaryKnobs[$KnobNo][12] = $DefaultValue
	EndIf
	$_RotaryKnobs[$KnobNo][41] = $_RotaryKnobs0Knobs And $_RotaryKnobs[$KnobNo][12] - $_RotaryKnobs[$KnobNo][10] = $_RotaryKnobs[$KnobNo][11] - $_RotaryKnobs[$KnobNo][12] 	; "0" knob
	$_RotaryKnobs[$KnobNo][13] = $Resolution
	If IsBool($SnapValue) Then $SnapValue = $Resolution
	$_RotaryKnobs[$KnobNo][14] = $SnapValue
	$_RotaryKnobs[$KnobNo][15] = $StartAngle
	$_RotaryKnobs[$KnobNo][16] = $EndAngle
	$_RotaryKnobs[$KnobNo][17] = 0		; if mouse pressed: mouse y on knob
	$_RotaryKnobs[$KnobNo][18] = 0
	$_RotaryKnobs[$KnobNo][19] = 0
	$_RotaryKnobs[$KnobNo][20] = GUICtrlCreateLabel("",$X,$Y,1,2*$_RotaryKnobsFontSize)
	If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][20],$GUI_DOCKALL)
	GUICtrlSetState(-1,$GUI_HIDE+$GUI_DISABLE)
	GUICtrlSetBkColor($_RotaryKnobs[$KnobNo][20],$_RotaryKnobsTest ? $_RotaryKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
	GUICtrlSetFont(-1,$_RotaryKnobsFontSize)
	If $_RotaryKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_RotaryKnobsFontColor)
	$_RotaryKnobs[$KnobNo][21] = False
	$_RotaryKnobs[$KnobNo][22] = ""
	$_RotaryKnobs[$KnobNo][23] = GUICtrlCreateLabel("",$X,$Y,$_RotaryKnobsWidth,$_RotaryKnobsHeight)
	If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][23],$GUI_DOCKALL)
	GUICtrlSetBkColor(-1,$GUI_BKCOLOR_TRANSPARENT)
	$_RotaryKnobs[$KnobNo][24] = False
	$_RotaryKnobs[$KnobNo][25] = ""
	$_RotaryKnobs[$KnobNo][26] = -1
	$_RotaryKnobs[$KnobNo][27] = -1
	$_RotaryKnobs[$KnobNo][28] = -1
	$_RotaryKnobs[$KnobNo][29] = -1
	$_RotaryKnobs[$KnobNo][30] = False
	$_RotaryKnobs[$KnobNo][31] = ""
	$_RotaryKnobs[$KnobNo][32] = True		; default knob enabled
	$_RotaryKnobs[$KnobNo][33] = True		; default knob shown or hidden
	$_RotaryKnobs[$KnobNo][34] = False		; if true knob is drawn
	$_RotaryKnobs[$KnobNo][35] = False		; if true knob is off (but enabled)
	$_RotaryKnobs[$KnobNo][36] = UBound($_RotaryKnobsGUI)-1
	$_RotaryKnobs[$KnobNo][37] = $Exponential
	$_RotaryKnobs[$KnobNo][38] = $ConversionRate
	$_RotaryKnobs[$KnobNo][39] = $ConversionDecimals
	$_RotaryKnobs[$KnobNo][40] = $_RotaryKnobsGUI[UBound($_RotaryKnobsGUI)-1][0]	; gui handle
	Return $_RotaryKnobs[$KnobNo][0]
EndFunc

; Adding labels to a rotary knob
; RotaryKnobLabels(
;	$Knob							Handle of the rotary knob
;	$Label = ""						Title/description string
;	$LabelPosition					Position of label, 0 = top high, 1 = top low, 2 = right, 3 = left, 4 = bottom high, 5 = bottom low
;	$Tooltip = ""					Tooltip string
;	$Left = ""						Left label string
;	$Right = ""						Right label string
;	$Top = ""						Top label string
;	$Bottom = ""					Bottom label string
;)
; Return values
;	True							Knob exists
;	False							Knob doesn't exist
Func RotaryKnobLabels($Knob,$Label = "",$LabelPosition = 0,$Tooltip = "",$Left = "",$Right = "",$Top = "",$Bottom = "",$Wide = False)		; add labels
	If Not IsArray($_RotaryKnobs) Then Return False

	Local $YAngleCorrection = 30,$X,$Y
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			If StringLen($Label) > 0 Then
				GUICtrlSetState($_RotaryKnobs[$KnobNo][20],$GUI_ENABLE+$GUI_SHOW)
				$_RotaryKnobs[$KnobNo][21] = True
				$_RotaryKnobs[$KnobNo][22] = $Label
				Switch $LabelPosition
					Case 1		; top low
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsWidth/2-$_RotaryKnobsTextWidth/2,$_RotaryKnobs[$KnobNo][2]-$_RotaryKnobsLabelRadius-1.2*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_CENTER)
					Case 2		; right
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsWidth+$_RotaryKnobsLabelRadius,$_RotaryKnobs[$KnobNo][2]+$_RotaryKnobsHeight/2-0.7*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_LEFT)
					Case 3		; left
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]-$_RotaryKnobsLabelRadius-$_RotaryKnobsTextWidth,$_RotaryKnobs[$KnobNo][2]+$_RotaryKnobsHeight/2-0.7*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_RIGHT)
					Case 4		; bottom high
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsWidth/2-$_RotaryKnobsTextWidth/2,$_RotaryKnobs[$KnobNo][2]+$_RotaryKnobsHeight+$_RotaryKnobsLabelRadius+0.8*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_CENTER)
					Case 5		; bottom low
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsWidth/2-$_RotaryKnobsTextWidth/2,$_RotaryKnobs[$KnobNo][2]+$_RotaryKnobsHeight+$_RotaryKnobsLabelRadius-0.2*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_CENTER)
					Case Else	; top high
						GUICtrlSetPos($_RotaryKnobs[$KnobNo][20],$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsWidth/2-$_RotaryKnobsTextWidth/2,$_RotaryKnobs[$KnobNo][2]-$_RotaryKnobsLabelRadius-2.8*$_RotaryKnobsFontSize,$_RotaryKnobsTextWidth,2*$_RotaryKnobsFontSize)
						GUICtrlSetStyle($_RotaryKnobs[$KnobNo][20],$SS_CENTER)
				EndSwitch
			EndIf
			If IsBool($Tooltip) Then $Tooltip = $_RotaryKnobsTextValue
			If StringLen($Tooltip) > 0 Then
				$_RotaryKnobs[$KnobNo][24] = True
				$_RotaryKnobs[$KnobNo][25] = $Tooltip
				_GUIToolTip_AddTool($_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][6],0,$Tooltip,GUICtrlGetHandle($_RotaryKnobs[$KnobNo][23]))
			EndIf
			If IsBool($Left) Then $Left = $_RotaryKnobs[$KnobNo][10]
			If StringLen($Left) > 0 Then
				$X = -2+$_RotaryKnobs[$KnobNo][1]+$_RotaryKnobsFontSize/3-$_RotaryKnobsLabelWidth+$_RotaryKnobsWidth/2+($_RotaryKnobsWidth/2+$_RotaryKnobsLabelRadius)*Cos(($_RotaryKnobs[$KnobNo][15]-90)*3.141592653589793/180)
				$Y = $_RotaryKnobs[$KnobNo][4]-$_RotaryKnobsFontSize*(180+$YAngleCorrection-Abs($_RotaryKnobs[$KnobNo][15]))/180+($_RotaryKnobsHeight/2+$_RotaryKnobsLabelRadius)*Sin(($_RotaryKnobs[$KnobNo][15]-90)*3.141592653589793/180)
				$Left = StringReplace($left,$_RotaryKnobsTextLowValue,$_RotaryKnobs[$KnobNo][10])
				$_RotaryKnobs[$KnobNo][26] = GUICtrlCreateLabel($Left,$X,$Y,$_RotaryKnobsLabelWidth,-1,$SS_RIGHT)
				If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][26],$GUI_DOCKALL)
				GUICtrlSetBkColor($_RotaryKnobs[$KnobNo][26],$_RotaryKnobsTest ? $_RotaryKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_RotaryKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_RotaryKnobsFontColor)
				GUICtrlSetFont(-1,$_RotaryKnobsFontSize-1)
			EndIf
			If IsBool($Right) Then $Right = $_RotaryKnobs[$KnobNo][11]
			If StringLen($Right) > 0 Then
				$X = 3+$_RotaryKnobs[$KnobNo][3]-$_RotaryKnobsFontSize/3+($_RotaryKnobsWidth/2+$_RotaryKnobsLabelRadius)*Cos(($_RotaryKnobs[$KnobNo][16]-90)*3.141592653589793/180)
				$Y = $_RotaryKnobs[$KnobNo][4]-$_RotaryKnobsFontSize*(180+$YAngleCorrection-Abs($_RotaryKnobs[$KnobNo][16]))/180+($_RotaryKnobsHeight/2+$_RotaryKnobsLabelRadius)*Sin(($_RotaryKnobs[$KnobNo][16]-90)*3.141592653589793/180)
				$Right = StringReplace($Right,$_RotaryKnobsTextHighValue,$_RotaryKnobs[$KnobNo][11])
				$_RotaryKnobs[$KnobNo][27] = GUICtrlCreateLabel($Right,$X,$Y,$_RotaryKnobsLabelWidth)
				If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][27],$GUI_DOCKALL)
				GUICtrlSetBkColor($_RotaryKnobs[$KnobNo][27],$_RotaryKnobsTest ? $_RotaryKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_RotaryKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_RotaryKnobsFontColor)
				GUICtrlSetFont(-1,$_RotaryKnobsFontSize-1)
			EndIf
			If IsBool($Top) Then $Top = ($_RotaryKnobs[$KnobNo][11]+$_RotaryKnobs[$KnobNo][10])/2
			If StringLen($Top) > 0 Then
				$Y = $_RotaryKnobs[$KnobNo][2]-$_RotaryKnobsFontSize-$_RotaryKnobsLabelRadius
				$_RotaryKnobs[$KnobNo][28] = GUICtrlCreateLabel($Top,$_RotaryKnobs[$KnobNo][3]-$_RotaryKnobsLabelWidth/2,$Y,$_RotaryKnobsLabelWidth,-1,$SS_CENTER)
				If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][28],$GUI_DOCKALL)
				GUICtrlSetBkColor($_RotaryKnobs[$KnobNo][28],$_RotaryKnobsTest ? $_RotaryKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_RotaryKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_RotaryKnobsFontColor)
				GUICtrlSetFont(-1,$_RotaryKnobsFontSize-1)
			EndIf
			If IsBool($Bottom) Then $Bottom = ($_RotaryKnobs[$KnobNo][11]+$_RotaryKnobs[$KnobNo][10])/2
			If StringLen($Bottom) > 0 Then
				$_RotaryKnobs[$KnobNo][30] = True
				$Y = $_RotaryKnobs[$KnobNo][2]+$_RotaryKnobsHeight+$_RotaryKnobsLabelRadius-0.2*$_RotaryKnobsFontSize
				$_RotaryKnobs[$KnobNo][31] = $Bottom
				$_RotaryKnobs[$KnobNo][29] = GUICtrlCreateLabel($Bottom,1+$_RotaryKnobs[$KnobNo][3]-$_RotaryKnobsLabelWidth/2,$Y,$_RotaryKnobsLabelWidth,Ceiling($_RotaryKnobsFontSize*1.5),$SS_CENTER)
				If $_RotaryKnobsControlsFixed Then GUICtrlSetResizing($_RotaryKnobs[$KnobNo][29],$GUI_DOCKALL)
				GUICtrlSetBkColor($_RotaryKnobs[$KnobNo][29],$_RotaryKnobsTest ? $_RotaryKnobsTestColor : $GUI_BKCOLOR_TRANSPARENT)
				If $_RotaryKnobsFontColor <> -1 Then GUICtrlSetColor(-1,$_RotaryKnobsFontColor)
				GUICtrlSetFont(-1,$_RotaryKnobsFontSize-1)
			EndIf
			_RotaryKnobReplaceText($KnobNo,True)
			Return True
		EndIf
	Next
	Return False
EndFunc

Func _RotaryKnobReplaceText($KnobNo,$Start = False)		; internal: insert values in text
	Local $Change = $Start Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextValue) > 0 Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextLowValue) > 0 Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextHighValue) > 0 Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextDefaultValue) > 0 Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextSnapValue) > 0 Or StringInStr($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextConversionValue) > 0

	If IsArray($_RotaryKnobs[$KnobNo][14]) Then
		If $Change And $_RotaryKnobs[$KnobNo][21] Then GUICtrlSetData($_RotaryKnobs[$KnobNo][20],StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]]),$_RotaryKnobsTextLowValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][10]]),$_RotaryKnobsTextHighValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][11]]),$_RotaryKnobsTextDefaultValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]]))
		If $_RotaryKnobs[$KnobNo][24] Then _GUIToolTip_UpdateTipText($_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][6],0,GUICtrlGetHandle($_RotaryKnobs[$KnobNo][23]),StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][25],$_RotaryKnobsTextValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]]),$_RotaryKnobsTextLowValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][10]]),$_RotaryKnobsTextHighValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][11]]),$_RotaryKnobsTextDefaultValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]]))
		If $_RotaryKnobs[$KnobNo][30] Then GUICtrlSetData($_RotaryKnobs[$KnobNo][29],StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][31],$_RotaryKnobsTextValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]]),$_RotaryKnobsTextLowValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][10]]),$_RotaryKnobsTextHighValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][11]]),$_RotaryKnobsTextDefaultValue,($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]]))
	Else
		If $Change And $_RotaryKnobs[$KnobNo][21] Then GUICtrlSetData($_RotaryKnobs[$KnobNo][20],StringReplace(StringReplace(StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][22],$_RotaryKnobsTextValue,$_RotaryKnobs[$KnobNo][14]*Round(($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18])/$_RotaryKnobs[$KnobNo][14])),$_RotaryKnobsTextLowValue,$_RotaryKnobs[$KnobNo][10]),$_RotaryKnobsTextHighValue,$_RotaryKnobs[$KnobNo][11]),$_RotaryKnobsTextDefaultValue,$_RotaryKnobs[$KnobNo][12]),$_RotaryKnobsTextSnapValue,$_RotaryKnobs[$KnobNo][14]),$_RotaryKnobsTextConversionValue,StringFormat("%." & $_RotaryKnobs[$KnobNo][39] & "f",$_RotaryKnobs[$KnobNo][38]*($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]))))
		If $_RotaryKnobs[$KnobNo][24] Then _GUIToolTip_UpdateTipText($_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][6],0,GUICtrlGetHandle($_RotaryKnobs[$KnobNo][23]),StringReplace(StringReplace(StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][25],$_RotaryKnobsTextValue,$_RotaryKnobs[$KnobNo][14]*Round(($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18])/$_RotaryKnobs[$KnobNo][14])),$_RotaryKnobsTextLowValue,$_RotaryKnobs[$KnobNo][10]),$_RotaryKnobsTextHighValue,$_RotaryKnobs[$KnobNo][11]),$_RotaryKnobsTextDefaultValue,$_RotaryKnobs[$KnobNo][12]),$_RotaryKnobsTextSnapValue,$_RotaryKnobs[$KnobNo][14]),$_RotaryKnobsTextConversionValue,StringFormat("%." & $_RotaryKnobs[$KnobNo][39] & "f",$_RotaryKnobs[$KnobNo][38]*($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]))))
		If $_RotaryKnobs[$KnobNo][30] Then GUICtrlSetData($_RotaryKnobs[$KnobNo][29],StringReplace(StringReplace(StringReplace(StringReplace(StringReplace(StringReplace($_RotaryKnobs[$KnobNo][31],$_RotaryKnobsTextValue,$_RotaryKnobs[$KnobNo][14]*Round(($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18])/$_RotaryKnobs[$KnobNo][14])),$_RotaryKnobsTextLowValue,$_RotaryKnobs[$KnobNo][10]),$_RotaryKnobsTextHighValue,$_RotaryKnobs[$KnobNo][11]),$_RotaryKnobsTextDefaultValue,$_RotaryKnobs[$KnobNo][12]),$_RotaryKnobsTextSnapValue,$_RotaryKnobs[$KnobNo][14]),$_RotaryKnobsTextConversionValue,StringFormat("%." & $_RotaryKnobs[$KnobNo][39] & "f",$_RotaryKnobs[$KnobNo][38]*($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]))))
	EndIf
EndFunc

; Enable or disable rotary knob
; RotaryKnobEnable
;	$Knob							Knob handle
;	$Enable = True					Enable or disable
; )
; Return values
;	True or False					Enabled or disabled
;	False							If knob doesn't exist
Func RotaryKnobEnable($Knob,$Enable = True)
	If Not IsArray($_RotaryKnobs) Then Return False
	If Not $_RotaryKnobsEnabled Then Return True

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			If ($_RotaryKnobs[$KnobNo][32] And $Enable) Or (Not $_RotaryKnobs[$KnobNo][32] And Not $Enable) Then ExitLoop
			$_RotaryKnobs[$KnobNo][32] = $Enable
			If $Enable Then
				If $_RotaryKnobs[$KnobNo][21] Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_RotaryKnobs[$KnobNo][20],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][20],$_RotaryKnobsFontColor)
					EndIf
				EndIf
				GUICtrlSetState($_RotaryKnobs[$KnobNo][23],$GUI_ENABLE)
				If $_RotaryKnobs[$KnobNo][26] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_RotaryKnobs[$KnobNo][26],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][26],$_RotaryKnobsFontColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][27] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_RotaryKnobs[$KnobNo][27],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][27],$_RotaryKnobsFontColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][28] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_RotaryKnobs[$KnobNo][28],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][28],$_RotaryKnobsFontColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][30] Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						GUICtrlSetState($_RotaryKnobs[$KnobNo][29],$GUI_ENABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][29],$_RotaryKnobsFontColor)
					EndIf
				EndIf
			Else
				If $_RotaryKnobs[$KnobNo][21] Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						If $_RotaryKnobsLabelsDisabled Then GUICtrlSetState($_RotaryKnobs[$KnobNo][20],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][20],$_RotaryKnobsFontDisabledColor)
					EndIf
				EndIf
				GUICtrlSetState($_RotaryKnobs[$KnobNo][23],$GUI_DISABLE)
				If $_RotaryKnobs[$KnobNo][26] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						If $_RotaryKnobsLabelsDisabled Then GUICtrlSetState($_RotaryKnobs[$KnobNo][26],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][26],$_RotaryKnobsFontDisabledColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][27] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						If $_RotaryKnobsLabelsDisabled Then GUICtrlSetState($_RotaryKnobs[$KnobNo][27],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][27],$_RotaryKnobsFontDisabledColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][28] <> -1 Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						If $_RotaryKnobsLabelsDisabled Then GUICtrlSetState($_RotaryKnobs[$KnobNo][28],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][28],$_RotaryKnobsFontDisabledColor)
					EndIf
				EndIf
				If $_RotaryKnobs[$KnobNo][30] Then
					If $_RotaryKnobsFontColor = -1 Or $_RotaryKnobsFontDisabledColor = -1 Then
						If $_RotaryKnobsLabelsDisabled Then GUICtrlSetState($_RotaryKnobs[$KnobNo][29],$GUI_DISABLE)
					Else
						GUICtrlSetColor($_RotaryKnobs[$KnobNo][29],$_RotaryKnobsFontDisabledColor)
					EndIf
				EndIf
			EndIf
			_RotaryKnobDraw($KnobNo)
			Return $Enable
		EndIf
	Next
	Return False
EndFunc

; Get if rotary knob is enabled or disabled
; RotaryKnobEnabled(
;	$Knob							Knob handle
; )
; Return values
;	True or False					Enabled or disabled
;	False							If knob doesn't exist
Func RotaryKnobEnabled($Knob)
	If Not IsArray($_RotaryKnobs) Then Return False
	If Not $_RotaryKnobsEnabled Then Return True

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			Return $_RotaryKnobs[$KnobNo][32]
		EndIf
	Next
	Return False
EndFunc

; Get if rotary knob is switch on or off
; RotaryKnobOn(
;	$Knob							Knob handle
; )
; Return values
;	True or False					On or off
;	False							If knob doesn't exist
Func RotaryKnobOn($Knob)
	If Not IsArray($_RotaryKnobs) Then Return False
	If Not $_RotaryKnobsEnabled Then Return True

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			Return $_RotaryKnobs[$KnobNo][35]
		EndIf
	Next
	Return False
EndFunc

; Show or hide rotary knob
; RotaryKnobShow
;	$Knob							Knob handle
;	$Show = True					Show or hide
; )
; Return values
;	True or False					Shown or hidden
;	False							If knob doesn't exist
Func RotaryKnobShow($Knob,$Show = True)
	If Not IsArray($_RotaryKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$_RotaryKnobs[$KnobNo][33] = $Show
			If $Show Then
				If $_RotaryKnobs[$KnobNo][21] Then GUICtrlSetState($_RotaryKnobs[$KnobNo][20],$GUI_SHOW)
				GUICtrlSetState($_RotaryKnobs[$KnobNo][23],$GUI_SHOW)
				If $_RotaryKnobs[$KnobNo][26] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][26],$GUI_SHOW)
				If $_RotaryKnobs[$KnobNo][27] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][27],$GUI_SHOW)
				If $_RotaryKnobs[$KnobNo][28] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][28],$GUI_SHOW)
				If $_RotaryKnobs[$KnobNo][30] Then GUICtrlSetState($_RotaryKnobs[$KnobNo][29],$GUI_SHOW)
			Else
				If $_RotaryKnobs[$KnobNo][21] Then GUICtrlSetState($_RotaryKnobs[$KnobNo][20],$GUI_HIDE)
				GUICtrlSetState($_RotaryKnobs[$KnobNo][23],$GUI_HIDE)
				If $_RotaryKnobs[$KnobNo][26] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][26],$GUI_HIDE)
				If $_RotaryKnobs[$KnobNo][27] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][27],$GUI_HIDE)
				If $_RotaryKnobs[$KnobNo][28] <> -1 Then GUICtrlSetState($_RotaryKnobs[$KnobNo][28],$GUI_HIDE)
				If $_RotaryKnobs[$KnobNo][30] Then GUICtrlSetState($_RotaryKnobs[$KnobNo][29],$GUI_HIDE)
			EndIf
			_RotaryKnobDraw($KnobNo)
			Return $Show
		EndIf
	Next
	Return False
EndFunc

; (Re)draw a rotary knob
; RotaryKnobDraw(
;	$Knob							Knob handle
; )
; Return values
;	True							Knob exists
;	False							Knob doesn't exists
Func RotaryKnobDraw($Knob)
	If Not IsArray($_RotaryKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

Func _RotaryKnobDraw($KnobNo)				; internal: draw rotary knob
	Local $DC,$BackColor,$Angle,$KnobGraphic = $_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][1]

	_GDIPlus_GraphicsClear($_RotaryKnobs[$KnobNo][6],0x00000000)
	_GDIPlus_GraphicsTranslateTransform($_RotaryKnobs[$KnobNo][6],3+$_RotaryKnobsWidth/2+0.5,3+$_RotaryKnobsHeight/2+0.5)
	$Angle = $_RotaryKnobs[$KnobNo][15]+($_RotaryKnobs[$KnobNo][16]-$_RotaryKnobs[$KnobNo][15])*($_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]-$_RotaryKnobs[$KnobNo][10])/($_RotaryKnobs[$KnobNo][11]-$_RotaryKnobs[$KnobNo][10])
	_GDIPlus_GraphicsRotateTransform($_RotaryKnobs[$KnobNo][6],$Angle)
	; draw dial back
	If Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32] Then
		_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobs[$KnobNo][41] ? $_RotaryKnobsDialBack0 : $_RotaryKnobsDialBack,-$_RotaryKnobsWidth/2-0.5,-$_RotaryKnobsHeight/2-0.5)
	Else
		_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsDialBackDisabled,-$_RotaryKnobsWidth/2-0.5,-$_RotaryKnobsHeight/2-0.5)
	EndIf
	_GDIPlus_GraphicsResetTransform($_RotaryKnobs[$KnobNo][6])
	; draw front
	If Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32] Then
		_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobs[$KnobNo][41] ? $_RotaryKnobsFront0 : $_RotaryKnobsFront,3+$_RotaryKnobsWidth/2-_GDIPlus_ImageGetWidth($_RotaryKnobsFront)/2,3+$_RotaryKnobsHeight/2-_GDIPlus_ImageGetHeight($_RotaryKnobsFront)/2)
	Else
		_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsFrontDisabled,3+$_RotaryKnobsWidth/2-_GDIPlus_ImageGetWidth($_RotaryKnobsFront)/2,3+$_RotaryKnobsHeight/2-_GDIPlus_ImageGetHeight($_RotaryKnobsFront)/2)
	EndIf
	_GDIPlus_GraphicsTranslateTransform($_RotaryKnobs[$KnobNo][6],3+$_RotaryKnobsWidth/2+0.5,3+$_RotaryKnobsHeight/2+0.5)
	If IsArray($_RotaryKnobsAnimation) And $KnobNo = $_RotaryKnobsAniFrames[0] And $_RotaryKnobsAniFrames[2] > -2 Then
		If $_RotaryKnobsAniFrames[2] > -1 Then
			_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsAnimation[$_RotaryKnobsAniFrames[2]][0],$_RotaryKnobsAnimation[$_RotaryKnobsAniFrames[2]][1],$_RotaryKnobsAnimation[$_RotaryKnobsAniFrames[2]][2])
			$_RotaryKnobsAniFrames[3] = $_RotaryKnobsAniFrames[2]
		Else
			$_RotaryKnobsAniFrames[0] = -1
			$_RotaryKnobsAniFrames[2] = -1
			$_RotaryKnobsAniFrames[3] = -1
		EndIf
	EndIf
	; draw off-image when knob has been switch off
	If (Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32]) And $_RotaryKnobsEnableOff And $_RotaryKnobs[$KnobNo][35] Then
		If IsArray($_RotaryKnobsOff) Then
			If $KnobNo = $_RotaryKnobsOffFrames[0] Then
				_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOff[$_RotaryKnobsOffFrames[2]][0],$_RotaryKnobsOff[$_RotaryKnobsOffFrames[2]][1],$_RotaryKnobsOff[$_RotaryKnobsOffFrames[2]][2])
				If $_RotaryKnobsOffFrames[2] <= 0 Then
					$_RotaryKnobsOffFrames[0] = -1
					$_RotaryKnobsOffFrames[3] = -1
				Else
					$_RotaryKnobsOffFrames[3] = $_RotaryKnobsOffFrames[2]
				EndIf
			Else
				_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOff[0][0],$_RotaryKnobsOff[0][1],$_RotaryKnobsOff[0][2])
			EndIf
		Else
			_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOff,$_RotaryKnobsOffX,$_RotaryKnobsOffY)
		EndIf
	Else
		; draw on-image when not default value
		If (Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32]) And $_RotaryKnobsEnableOn And $_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18] <> $_RotaryKnobs[$KnobNo][12] Then
			If IsArray($_RotaryKnobsOn) Then
				If $KnobNo = $_RotaryKnobsOnFrames[0] Then
					_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOn[$_RotaryKnobsOnFrames[2]][0],$_RotaryKnobsOn[$_RotaryKnobsOnFrames[2]][1],$_RotaryKnobsOn[$_RotaryKnobsOnFrames[2]][2])
					If $_RotaryKnobsOnFrames[2] <= 0 Then
						$_RotaryKnobsOnFrames[0] = -1
						$_RotaryKnobsOnFrames[3] = -1
					Else
						$_RotaryKnobsOnFrames[3] = $_RotaryKnobsOnFrames[2]
					EndIf
				Else
					_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOn[0][0],$_RotaryKnobsOn[0][1],$_RotaryKnobsOn[0][2])
				EndIf
			Else
				_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsOn,$_RotaryKnobsOnX,$_RotaryKnobsOnY)
			EndIf
		EndIf
 		; draw front dial
		_GDIPlus_GraphicsRotateTransform($_RotaryKnobs[$KnobNo][6],$Angle)
		If Not $_RotaryKnobsEnabled Or $_RotaryKnobs[$KnobNo][32] Then
			_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobs[$KnobNo][41] ? $_RotaryKnobsDialFront0 : $_RotaryKnobsDialFront,$_RotaryKnobs[$KnobNo][7],$_RotaryKnobs[$KnobNo][8])
		Else
			_GDIPlus_GraphicsDrawImage($_RotaryKnobs[$KnobNo][6],$_RotaryKnobsDialFrontDisabled,$_RotaryKnobs[$KnobNo][7],$_RotaryKnobs[$KnobNo][8])
		EndIf
	EndIf
	_GDIPlus_GraphicsResetTransform($_RotaryKnobs[$KnobNo][6])

	If $_RotaryKnobs[$KnobNo][34] Then
		$_RotaryKnobs[$KnobNo][34] = False				; indicate knob is erased
		If $_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][7] = 0 Then
			Local $DC = _WinAPI_GetDC($_RotaryKnobs[$KnobNo][40]),$BackColor = 0
			If $DC <> 0 Then
				$BackColor = _WinAPI_GetBkColor($DC)
				_WinAPI_ReleaseDC($_RotaryKnobs[$KnobNo][40],$DC)
				If $BackColor = -1 Then
					$BackColor = _WinAPI_GetSysColor(4)				; color probably same as background color of popup menu $COLOR_MENU ($COLOR_WINDOW gives wrong color)
					If $BackColor = 0 Then $BackColor = 15790320	; fail save in case black is returned
				EndIf
			EndIf
			$_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][7] = _GDIPlus_BrushCreateSolid(0xFF000000+$BackColor)
		EndIf
		_GDIPlus_GraphicsFillEllipse($KnobGraphic,$_RotaryKnobs[$KnobNo][1]-5,$_RotaryKnobs[$KnobNo][2]-5,$_RotaryKnobsWidth+10,$_RotaryKnobsHeight+10,$_RotaryKnobsGUI[$_RotaryKnobs[$KnobNo][36]][7])
	EndIf
	If Not $_RotaryKnobs[$KnobNo][33] Then Return		; rotary knob is hidden
	$_RotaryKnobs[$KnobNo][34] = True					; indicate knob is drawn
	_GDIPlus_GraphicsDrawImage($KnobGraphic,$_RotaryKnobs[$KnobNo][5],$_RotaryKnobs[$KnobNo][1]-3,$_RotaryKnobs[$KnobNo][2]-3)
EndFunc

; Let user dial selected rotary knob
; RotaryKnobDial()
; Return values
;	Knob handle						Knob being dialed
;	-1								No knob being dialed
Func RotaryKnobDial()
	If Not IsArray($_RotaryKnobs) Then Return -1
	If $_RotaryKnob > -1 Then
		Local $aCursor = GUIGetCursorInfo($_RotaryKnobs[$_RotaryKnob][40]),$Value
		If IsArray($_RotaryKnobs[$_RotaryKnob][14]) = 1 Then
			$Value = Round(($_RotaryKnobs[$_RotaryKnob][17]-Round($aCursor[1]))*$_RotaryKnobs[$_RotaryKnob][13])	; calculate delta for values array
		ElseIf $_RotaryKnobs[$_RotaryKnob][37] Then
			If ($_RotaryKnobs[$_RotaryKnob][17]-Round($aCursor[1])) > 0 Then
				$Value = $_RotaryKnobs[$_RotaryKnob][13]^($_RotaryKnobs[$_RotaryKnob][17]-Round($aCursor[1]))-1
			Else
				$Value = -($_RotaryKnobs[$_RotaryKnob][13]^(Round($aCursor[1])-$_RotaryKnobs[$_RotaryKnob][17])-1)
			EndIf
			$Value = $_RotaryKnobs[$_RotaryKnob][14]*Round($Value/$_RotaryKnobs[$_RotaryKnob][14])			; snap delta
		Else
			$Value = ($_RotaryKnobs[$_RotaryKnob][17]-Round($aCursor[1]))*$_RotaryKnobs[$_RotaryKnob][13]	; calculate delta
			$Value = $_RotaryKnobs[$_RotaryKnob][14]*Round($Value/$_RotaryKnobs[$_RotaryKnob][14])			; snap delta
		EndIf
		; delta not large than maximum or minimum delta
		If $Value > $_RotaryKnobs[$_RotaryKnob][11]-$_RotaryKnobs[$_RotaryKnob][9] Then $Value = $_RotaryKnobs[$_RotaryKnob][11]-$_RotaryKnobs[$_RotaryKnob][9]
		If $Value < $_RotaryKnobs[$_RotaryKnob][10]-$_RotaryKnobs[$_RotaryKnob][9] Then $Value = $_RotaryKnobs[$_RotaryKnob][10]-$_RotaryKnobs[$_RotaryKnob][9]
		If $_RotaryKnobs[$_RotaryKnob][18] <> $Value Then	; delta changed?
			If $_RotaryKnobs[$_RotaryKnob][9]+$_RotaryKnobs[$_RotaryKnob][18] = $_RotaryKnobs[$_RotaryKnob][12] Then _RotaryKnobsOnStart($_RotaryKnob)
			$_RotaryKnobs[$_RotaryKnob][18] = $Value
			_RotaryKnobReplaceText($_RotaryKnob)
			_RotaryKnobDraw($_RotaryKnob)
		EndIf
		Return $_RotaryKnobs[$_RotaryKnob][0]
	EndIf
	Return -1
EndFunc

; Current rotary knob being dialed
; RotaryKnobDialing()
; Return value						Knob currently being dialed
Func RotaryKnobDialing()
	If $_RotaryKnob = -1 Then Return -1
	Return $_RotaryKnobs[$_RotaryKnob][0]
EndFunc

; Check if user has stopped dailing
; RotaryKnobCheckStopped(
;	$aMsg							GUI message
; )
; Return value
;	True							Stopped
;	False							Still dialing
Func RotaryKnobCheckStopped(ByRef $aMsg)
	If $_RotaryKnob = -1 Then Return False
	Local $CursorInfo = GUIGetCursorInfo($aMsg[1])
;~ 	Return $aMsg[0] = $GUI_EVENT_PRIMARYUP Or ($aMsg[0] <> 0 And $aMsg[0] <> $GUI_EVENT_MOUSEMOVE And $aMsg[0] <> $GUI_EVENT_PRIMARYDOWN) Or Not WinActive($_RotaryKnobs[$_RotaryKnob][40])
	Return $CursorInfo[2] = 0 Or $aMsg[0] = $GUI_EVENT_PRIMARYUP Or ($aMsg[0] <> 0 And $aMsg[0] <> $GUI_EVENT_MOUSEMOVE And $aMsg[0] <> $GUI_EVENT_PRIMARYDOWN) Or Not WinActive($_RotaryKnobs[$_RotaryKnob][40])
EndFunc

; User stopped dialing, so reset for the next dial
; RotaryKnobStopDial()
; Return value						Dialed knob
Func RotaryKnobStopDial()
	If Not IsArray($_RotaryKnobs) Or $_RotaryKnob = -1 Or $_RotaryKnobs[$_RotaryKnob][35] Then
		$_RotaryKnob = -1
		Return -1
	EndIf
	Local $Knob
	$_RotaryKnobs[$_RotaryKnob][9] += $_RotaryKnobs[$_RotaryKnob][18]
	$_RotaryKnobs[$_RotaryKnob][18] = 0
	$Knob = $_RotaryKnobs[$_RotaryKnob][0]
	_RotaryKnobsAnimateStart($_RotaryKnob)
	$_RotaryKnob = -1
	Return $Knob
EndFunc

; Reset rotary knob to default value
; RotaryKnobReset(
;	$Knob							Knob handle
; )
; Return values
;	True							Rotary knob resetted
;	False							If knob doesn't exist
Func RotaryKnobReset($Knob)
	If Not IsArray($_RotaryKnobs) Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			If IsArray($_RotaryKnobs[$KnobNo][14]) Then
				$_RotaryKnobs[$KnobNo][9] = 0
				For $ValueNo = 0 To UBound($_RotaryKnobs[$KnobNo][14])-1
					If ($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]] >= ($_RotaryKnobs[$KnobNo][14])[$ValueNo] Then	; take value if larger
						$_RotaryKnobs[$KnobNo][9] = $ValueNo
					Else
						ExitLoop
					EndIf
				Next
			Else
				$_RotaryKnobs[$KnobNo][9] = $_RotaryKnobs[$KnobNo][12]
			EndIf
			_RotaryKnobReplaceText($KnobNo)
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Switch rotary knob on or off
; RotaryKnobSwitch(
;	$Knob							Knob handle
;	$Switch = True					True = on, False = off
; )
; Return values
;	True							Rotary knob switched
;	False							If knob doesn't exist or off mechanism isn't enabled
Func RotaryKnobSwitch($Knob,$Switch = True)
	If Not IsArray($_RotaryKnobs) Then Return False
	If Not $_RotaryKnobsEnableOff Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$_RotaryKnobs[$KnobNo][35] = Not $Switch	; true = off
			_RotaryKnobReplaceText($KnobNo)
			If $_RotaryKnobs[$KnobNo][35] Then
				_RotaryKnobsOffStart($KnobNo)
			ElseIf $_RotaryKnobs[$KnobNo][9] <> $_RotaryKnobs[$KnobNo][12] Then
				_RotaryKnobsOnStart($KnobNo)
			EndIf
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Toggle rotary knob on or off
; RotaryKnobToggle(
;	$Knob							Knob handle
; )
; Return values
;	True							Rotary knob toggled
;	False							If knob doesn't exist or off mechanism isn't enabled
Func RotaryKnobToggle($Knob)
	If Not IsArray($_RotaryKnobs) Then Return False
	If Not $_RotaryKnobsEnableOff Then Return False

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$_RotaryKnobs[$KnobNo][35] = Not $_RotaryKnobs[$KnobNo][35]
			_RotaryKnobReplaceText($KnobNo)
			If $_RotaryKnobs[$KnobNo][35] Then
				_RotaryKnobsOffStart($KnobNo)
			ElseIf $_RotaryKnobs[$KnobNo][9] <> $_RotaryKnobs[$KnobNo][12] Then
				_RotaryKnobsOnStart($KnobNo)
			EndIf
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Set rotary knob value
; RotaryKnobSetValue(
;	$Knob							Knob handle
;	$Value							Knob value
; )
; Return values
;	True							Rotary knob set
;	False							If knob doesn't exist
Func RotaryKnobSetValue($Knob,$Value,$Animation = False)
	If Not IsArray($_RotaryKnobs) Then Return False

	Local $Previous
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$Previous = $_RotaryKnobs[$KnobNo][9]
			If $Value > $_RotaryKnobs[$KnobNo][11] Then
				$Value = $_RotaryKnobs[$KnobNo][11]
			ElseIf $Value < $_RotaryKnobs[$KnobNo][10] Then
				$Value = $_RotaryKnobs[$KnobNo][10]
			EndIf
			If IsArray($_RotaryKnobs[$KnobNo][14]) Then
				$_RotaryKnobs[$KnobNo][9] = 0
				For $ValueNo = 0 To UBound($_RotaryKnobs[$KnobNo][14])-1
					If $Value >= ($_RotaryKnobs[$KnobNo][14])[$ValueNo] Then	; take value if larger
						$_RotaryKnobs[$KnobNo][9] = $ValueNo
					Else
						ExitLoop
					EndIf
				Next
			Else
				$_RotaryKnobs[$KnobNo][9] = $Value
			EndIf
			If $Animation And $Previous = $_RotaryKnobs[$KnobNo][12] Then _RotaryKnobsOnStart($KnobNo)
			_RotaryKnobReplaceText($KnobNo)
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Increase rotary knob value
; RotaryKnobIncreaseValue(
;	$Knob							Knob handle
;	$Ratio = 1						Increase ratio of knob resolution
;	$CheckEnabled = False			True = if knob has been disabled value isn't increased
;	$CheckOff = false				True = if knob has been switched off value isn't increased
; )
; Return values
;	True							Rotary knob set
;	False							If knob doesn't exist
Func RotaryKnobIncreaseValue($Knob,$Ratio = 1,$CheckEnabled = False,$CheckOff = False)
	If Not IsArray($_RotaryKnobs) Then Return False

	Local $Previous
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $CheckEnabled And $_RotaryKnobsEnabled And Not $_RotaryKnobs[$KnobNo][32] Then Return False
		If $CheckOff And ($_RotaryKnobsEnabled And Not $_RotaryKnobs[$KnobNo][32]) Or $_RotaryKnobsEnableOff And $_RotaryKnobs[$KnobNo][35] Then Return False
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$Previous = $_RotaryKnobs[$KnobNo][9]
			If IsArray($_RotaryKnobs[$KnobNo][14]) Then
				$_RotaryKnobs[$KnobNo][9] += $Ratio
			Else
				$_RotaryKnobs[$KnobNo][9] += $_RotaryKnobs[$KnobNo][14]*$Ratio
				$_RotaryKnobs[$KnobNo][9] = $_RotaryKnobs[$KnobNo][14]*Round($_RotaryKnobs[$KnobNo][9]/$_RotaryKnobs[$KnobNo][14])
			EndIf
			If $_RotaryKnobs[$KnobNo][9] > $_RotaryKnobs[$KnobNo][11] Then $_RotaryKnobs[$KnobNo][9] = $_RotaryKnobs[$KnobNo][11]
			If $Previous = $_RotaryKnobs[$KnobNo][12] Then _RotaryKnobsOnStart($KnobNo)
			_RotaryKnobReplaceText($KnobNo)
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Decrease rotary knob value
; RotaryKnobDecreaseValue(
;	$Knob							Knob handle
;	$Ratio = 1						Decrease ratio of knob resolution
;	$CheckEnabled = False			True = if knob has been disabled value isn't decreased
;	$CheckOff = false				True = if knob has been switched off value isn't decreased
; )
; Return values
;	True							Rotary knob set
;	False							If knob doesn't exist
Func RotaryKnobDecreaseValue($Knob,$Ratio = 1,$CheckEnabled = False,$CheckOff = False)
	If Not IsArray($_RotaryKnobs) Then Return False

	Local $Previous
	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $CheckEnabled And $_RotaryKnobsEnabled And Not $_RotaryKnobs[$KnobNo][32] Then Return False
		If $CheckOff And ($_RotaryKnobsEnabled And Not $_RotaryKnobs[$KnobNo][32]) Or $_RotaryKnobsEnableOff And $_RotaryKnobs[$KnobNo][35] Then Return False
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			$Previous = $_RotaryKnobs[$KnobNo][9]
			If IsArray($_RotaryKnobs[$KnobNo][14]) Then
				$_RotaryKnobs[$KnobNo][9] -= $Ratio
			Else
				$_RotaryKnobs[$KnobNo][9] -= $_RotaryKnobs[$KnobNo][14]*$Ratio
				$_RotaryKnobs[$KnobNo][9] = $_RotaryKnobs[$KnobNo][14]*Round($_RotaryKnobs[$KnobNo][9]/$_RotaryKnobs[$KnobNo][14])
			EndIf
			If $_RotaryKnobs[$KnobNo][9] < $_RotaryKnobs[$KnobNo][10] Then $_RotaryKnobs[$KnobNo][9] = $_RotaryKnobs[$KnobNo][10]
			If $Previous = $_RotaryKnobs[$KnobNo][12] Then _RotaryKnobsOnStart($KnobNo)
			_RotaryKnobReplaceText($KnobNo)
			_RotaryKnobDraw($KnobNo)
			Return True
		EndIf
	Next
	Return False
EndFunc

; Get rotary knob value
; RotaryKnobGetValue(
;	$Knob							Knob handle
; )
; Return values
;	Knob value						Value of rotary knob in defined range
;	0								If knob doesn't exist
Func RotaryKnobGetValue($Knob)
	If Not IsArray($_RotaryKnobs) Then Return 0

	For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
		If $_RotaryKnobs[$KnobNo][0] = $Knob Then
			If IsArray($_RotaryKnobs[$KnobNo][14]) Then
				If $_RotaryKnobsEnableOff And $_RotaryKnobs[$KnobNo][35] Then
					Return ($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]]
				Else
					Return ($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]]
				EndIf
			Else
				If $_RotaryKnobsEnableOff And $_RotaryKnobs[$KnobNo][35] Then
					Return $_RotaryKnobs[$KnobNo][12]	; default value when knob has been switch off
				Else
					Return $_RotaryKnobs[$KnobNo][9]+$_RotaryKnobs[$KnobNo][18]
				EndIf
			EndIf
		EndIf
	Next
	Return 0
EndFunc

; Get rotary knob default value
; RotaryKnobGetDefaultValue(
;	$Knob							Knob handle
; )
; Return values
;	Knob value						Default value of rotary knob
;	0								If knob doesn't exist
Func RotaryKnobGetDefaultValue($Knob)
	If Not IsArray($_RotaryKnobs) Then Return 0

	If IsArray($_RotaryKnobs[$Knob][14]) Then
		For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
			If $_RotaryKnobs[$KnobNo][0] = $Knob Then Return ($_RotaryKnobs[$KnobNo][14])[$_RotaryKnobs[$KnobNo][12]]
		Next
	Else
		For $KnobNo = 0 To Ubound($_RotaryKnobs)-1
			If $_RotaryKnobs[$KnobNo][0] = $Knob Then Return $_RotaryKnobs[$KnobNo][12]
		Next
	EndIf
	Return 0
EndFunc

; Don't use, for backwards compatibility only
Func RotaryKnobGetGUI($GUIno = 0)
	Return $_RotaryKnobsGUI[$GUIno][0]
EndFunc