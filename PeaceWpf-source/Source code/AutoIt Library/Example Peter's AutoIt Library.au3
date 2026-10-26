#include "Window, Screen, Mouse and Control.au3"
#include "Logics and Math.au3"
#include "String and File String.au3"
#include "Dialogue.au3"
#include "Lists.au3"
#include "Miscellaneous.au3"
; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.23
; Description ...: Small functions example
; Author(s) .....: Peter Verbeek
; Version history: 1.30		added: coloring of window title and background
;                  1.23		improved: functions used which use the Desktop Window Manager window coordinates, Windows Vista and higher
;                  1.22		added: hide message dialogue shows in console when closed by window close button
;                  1.19		added: F1 help example
;                  1.16		added: clickable Pal icon
;                  			change: link control now does go to Pal website
;                  			added: link control to go to Peace website
;                  1.12		improved: hide message dialogue now includes a warning icon as an example
;                  1.00		initial version
; ===============================================================================================================================

GUI()

Func GUI()
	Const $GUIMargin = 10,$GUIWidth = 415,$GUIHeight = 260,$ButtonWidth = 95,$ButtonHeight = 22,$ControlHeight = 20
	Local $GUI,$aMsg,$Icon,$ControlYOffset,$LabelWindowPosition,$LabelGUIMousePosition,$LabelMousePosition,$LabelLink1,$LabelLink2,$Bar,$AudioPanel,$Close,$Ok,$Sure,$Options,$Message,$MessageHidden = False,$On,$Off

	$GUI = GUICreate("Functions example of Peter's AutoIt Library",$GUIWidth,$GUIHeight)
	_WindowDWMSetColors($GUI,0xC0D0FF,0)
	GUISetBkColor(0xD0E0FF)
	_GDIPlus_StartUp()						; initialize graphics system
	_GraphicButtons()						; initialize graphic buttons

	$Icon = _GraphicButton(@ScriptDir & "\PalIcon.png",0,"Show Peter's AutoIt Library website",$GUIWidth-$GUIMargin-65,$GUIMargin)

	; show some values on window, desktop and mouse
	$ControlYOffset = $GUIMargin
	GUICtrlCreateLabel("Window width " & _WindowDWMWidth($GUI) & " and height " & _WindowDWMHeight($GUI) & " (Desktop Window Manager)",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin-_GraphicButtonWidth($Icon),$ControlHeight)
	$ControlYOffset += $ControlHeight
	GUICtrlCreateLabel("Window client width " & _WindowClientWidth($GUI) & " and height " & _WindowClientHeight($GUI),$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin-_GraphicButtonWidth($Icon),$ControlHeight)
	$ControlYOffset += $ControlHeight
	GUICtrlCreateLabel("Window borders width " & _WindowBordersWidth() & " and height " & _WindowBordersHeight(),$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin-_GraphicButtonWidth($Icon),$ControlHeight)
	$ControlYOffset += $ControlHeight
	GUICtrlCreateLabel("Desktop work area width " & _DesktopWorkAreaWidth() & " and height " & _DesktopWorkAreaHeight(),$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin-_GraphicButtonWidth($Icon),$ControlHeight)
	$ControlYOffset += $ControlHeight
	$LabelWindowPosition = GUICtrlCreateLabel("",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin,$ControlHeight)
	$ControlYOffset += $ControlHeight
	$LabelGUIMousePosition = GUICtrlCreateLabel("",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin,$ControlHeight)
	$ControlYOffset += $ControlHeight
	$LabelMousePosition = GUICtrlCreateLabel("",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin,$ControlHeight)

	; create link label
	$ControlYOffset += $ControlHeight
	$LabelLink1 = _GUICtrlLinkLabel_Create("Show Pal (Peter's AutoIt Library) website",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin,$ControlHeight)
	GUICtrlSetTip(-1,"Click to show the Pal website in the default browser")
	$ControlYOffset += $ControlHeight
	$LabelLink2 = _GUICtrlLinkLabel_Create("Show Peace (Peter's Equalizer APO Configuration Extension) website",$GUIMargin,$ControlYOffset,$GUIWidth-2*$GUIMargin,$ControlHeight)
	GUICtrlSetTip(-1,"Click to show the Peace website in the default browser")
	; progress bar example
	$Bar = GUICtrlCreateButton("Progress bars",$GUIMargin+100,$GUIHeight-2*($GUIMargin+$ControlHeight),$ButtonWidth,$ButtonHeight)
	GUICtrlSetTip(-1,"Activate a progress bar")
	Global $AudioPanel = GUICtrlCreateButton("Audio panel",$GUIMargin+200,$GUIHeight-2*($GUIMargin+$ControlHeight),$ButtonWidth,$ButtonHeight)
	GUICtrlSetTip(-1,"Open Windows audio devices panel")

	; create graphical switch
	$On = _GraphicButton(@ScriptDir & "\on3.png",0,"Click to switch off",$GUIMargin,$GUIHeight-2*($GUIMargin+$ControlHeight),80,$ButtonHeight)
	$Off = _GraphicButton(@ScriptDir & "\off3.png",0,"Click to switch on",$GUIMargin,$GUIHeight-2*($GUIMargin+$ControlHeight),80,$ButtonHeight)
	GUICtrlSetState(-1,$GUI_HIDE)

	; create buttons with some png images
	$Close = _GraphicButton(@ScriptDir & "\close.png","Close","Click to close example",$GUIMargin,$GUIHeight-$GUIMargin-$ButtonHeight,$ButtonWidth,$ButtonHeight)
	$Ok = _GraphicButton(@ScriptDir & "\exclamationmark.png","Ok dialog","",$GUIMargin+100,$GUIHeight-$GUIMargin-$ButtonHeight,$ButtonWidth,$ButtonHeight)
	$Sure = _GraphicButton(@ScriptDir & "\questionmark.png","Sure dialog","",$GUIMargin+200,$GUIHeight-$GUIMargin-$ButtonHeight,$ButtonWidth,$ButtonHeight)

	$Options = GUICtrlCreateButton("Options dialog",$GUIMargin+300,$GUIHeight-$GUIMargin-$ButtonHeight,$ButtonWidth,$ButtonHeight)
	GUICtrlSetTip(-1,"Message dialog with own text on buttons")
	$Message = GUICtrlCreateButton("Info dialog",$GUIMargin+300,$GUIHeight-2*($GUIMargin+$ControlHeight),$ButtonWidth,$ButtonHeight)
	GUICtrlSetTip(-1,"Information dialog which can be hidden by user")

	GUISetState(@SW_SHOW,$GUI)
	_CHMShowOnF1("Pal")						; show PAL function library info
	While 1
		$aMsg = GUIGetMsg(1)
		If _ProgressBarActive() Then		; check if progress bar is on screen
			_ProgressBarAdd(0.2)
			If _Between(_ProgressBarGet(),90,90.2) Then _Console("nearly there")
			If _ProgressBarFull() Then		; when progress bar is 100% perform some actions
				_ProgressBarDestroy(500)	; show 100% for 0.5 sec then destroy
				_ProgressBarType(0)			; reset to default Windows progress bar (don't do this during a progress bar run)
			EndIf
		EndIf
		If $aMsg[0] = 0 Then ContinueLoop	; loop if there isn't an user interaction
		; show some coordinates
		GUICtrlSetData($LabelWindowPosition,"Window X position " & _WindowGetX($GUI,True) & " and Y position " & _WindowGetY($GUI,True) & " (DWM), move window to see change")
		GUICtrlSetData($LabelGUIMousePosition,"GUI mouse X position " & _GUIMouseGetX($GUI) & " and Y position " & _GUIMouseGetY($GUI) & ", move mouse to see change")
		GUICtrlSetData($LabelMousePosition,"Mouse X position " & MouseGetPos(0) & " and Y position " & MouseGetPos(1) & ", move mouse to see change")
		Switch $aMsg[0]
			Case $Icon
				ShellExecute("https://sourceforge.net/projects/peter-s-autoit-library")
			Case $LabelLink1
				_GUICtrlLinkLabel_Clicked($LabelLink1,"https://sourceforge.net/projects/peter-s-autoit-library")
			Case $LabelLink2
				_GUICtrlLinkLabel_Clicked($LabelLink2,"https://sourceforge.net/projects/peace-equalizer-apo-extension")
			Case $Bar
				If _ProgressBarActive() Then _ProgressBarDestroy()				; destroy progress bar to show new one
				; Show message box to chose progress bar type with tooltips on the buttons
				Switch _MessageBox("Choose progress bar type: horizontal or vertical","",0,"Horizontal 1|Default Windows horizontal progress bar","Horizontal 2|Windows 10 blue rectangle with moving percentage","Vertical 1|Default Windows vertical progress bar","Vertical 2|Red on white rectangle with moving percentage")
					Case 1
						_ProgressBarCreate("Progress bar","Please wait ...")	; show horizontal progress bar
					Case 2
						_ProgressBarType(1)										; bar with moving percentage
						_ProgressBarCreate("Progress bar","Please wait ...")	; show horizontal progress bar
					Case 3
						_ProgressBarCreate("Bar"," VU",True,False)				; show vertical progress bar
					Case 4
						_ProgressBarType(1,0xC00000,0xFFFFFF)					; bar with moving percentage
						_ProgressBarCreate("Bar"," VU",True,False)				; show vertical progress bar
				EndSwitch
			Case $AudioPanel
				_OpenAudioPanel()
			Case $On
				; switch off
				GUICtrlSetState($On,$GUI_HIDE)
				GUICtrlSetState($Off,$GUI_SHOW)
			Case $Off
				; switch on
				GUICtrlSetState($On,$GUI_SHOW)
				GUICtrlSetState($Off,$GUI_HIDE)
			Case $Ok
				_Ok("Ok dialogue","Message")
			Case $Sure
				_Sure("Are you sure dialogue","Question")
			Case $Options
				_Ok("You have chosen option " & _MessageBox("Choose an option","Make a choice",0,"Option 1","Option 2","Option 3","Option 4"))
			Case $Message
				If $MessageHidden Then _Ok("The user don't want to see the following message any longer")
				ConsoleWrite(_Message("This is important information, isn't it?",$MessageHidden,"Information","Hide this message","Fine","Important.ico",0) & @CRLF)	; write to console if closed or not
			Case $GUI_EVENT_CLOSE,$Close
				ExitLoop
		EndSwitch
	WEnd
	_GraphicButtons(False)					; release button image resources
	_GDIPlus_Shutdown()						; shutdown GDI+
	GUIDelete($GUI)
EndFunc
