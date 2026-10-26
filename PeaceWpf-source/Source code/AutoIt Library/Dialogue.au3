#include-once
#include <AutoItConstants.au3>
#include <MsgBoxConstants.au3>
#include <GUIConstantsEx.au3>
#include <WindowsConstants.au3>
#include <InetConstants.au3>
#include <ProgressConstants.au3>
#include <ColorConstants.au3>

; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.30
; Description....: Dialogue Functions
; Author(s)......: Peter Verbeek
; Version history: 1.29		bug: On the _Message() popup the "hide this message" label overlapped the Ok button a bit
;                  1.23		improved: For _MessageBox(), _HideMessage() and _Message() a text and background color can be specified
;                  			improved: Window a little bit enlarged of _HideMessage() and _Message() for longer text message
;                  1.22		improved: _Message() returns when the hide message dialogue has been closed by window close button
;                  1.16		improved: calling _ProgressBarType() when bar color set to -1 default color is used
;                  1.12		improved: icon can be shown in _Message() and _HideMessage() for warning purposes for instance
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;	_DialogueTitle
;	_Sure
;	_Ok
;	_Try
;	_MessageBox
;	_HideMessage
;	_Message
;	_DownloadFile
;	_OpenAudioPanel
;	_ProgressBarType
;	_ProgressBarCreate
;	_ProgressBarActive
;	_ProgressBarSet
;	_ProgressBarAdd
;	_ProgressBarGet
;	_ProgressBarFull
;	_ProgressBarEmpty
;	_ProgressBarDestroy
; ===============================================================================================================================

; #INTERNAL_USE_ONLY# ===========================================================================================================
;__ProgressBarSet()
; ===============================================================================================================================

; List of 18 functions
;	_DialogueTitle					Message dialogue		Sets common title of all dialogues
;	_Sure							Message dialogue		Are you sure dialogue with a Yes and a No button
;	_Ok								Message dialogue		Ok message with an Ok button
;	_Try							Message dialogue		Try dialogue with a Try and a Cancel button
;	_MessageBox						Message dialogue		Shows a message with maximum of 4 own buttons
;	_HideMessage					Message dialogue		Shows a message which can be hidden, hide message checkbox state is returned
;	_Message						Message dialogue		Shows a message which can be hidden, hide message checkbox state by passing reference to variable
;	_DownloadFile					Special dialogue		Download file dialogue, downloads a file from given url
;	_OpenAudioPanel					Windows dialogue		Opens the Windows audio panel with the play back and record devices
;	_ProgressBarType				Progress bar			Sets progress bar type to Windows default or colored rectangle with moving percentage
;	_ProgressBarCreate				Progress bar			Creates a sizable horizontal or vertical progress bar (which must be destroyed by _ProgressBarDestroy)
;	_ProgressBarActive				Progress bar			Returns if progress bar is currently active
;	_ProgressBarSet					Progress bar			Sets progress bar percentage
;	_ProgressBarAdd					Progress bar			Adds a percentage to progress bar
;	_ProgressBarGet					Progress bar			Gets progress bar percentage
;	_ProgressBarFull				Progress bar			Tests if progress bar is 100%
;	_ProgressBarEmpty				Progress bar			Tests if progress bar is 0%
;	_ProgressBarDestroy				Progress bar			Destroys progress bar GUI after some waiting (to show completion)

; #FUNCTION# ====================================================================================================================
; Name...........: _DialogueTitle
; Description....: Sets common title of all dialogues
; Syntax.........: _DialogueTitle($Title)
; Parameters.....: $Title					- Text of title, for instance program name
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _DialogueTitle($Title)
	Assign("_sDialogueTitle",$Title,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Sure
; Description....: Are you sure dialogue with a Yes and a No button
; Syntax.........: _Sure($sText [,$sTitle = "" [,$nTimeout = 0 [,$hGUI = -1]]])
; Parameters.....: $sText					- "Are you sure"-question
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $nTimeout				- Time out in seconds
;                  $hGUI					- Window GUI handle
; Return values..: True = Yes sure, False = No
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Sure($sText = "",$sTitle = "",$nTimeOut = 0,$hGUI = -1)
	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sText) = 0 Then $sText = "Are you sure?"
	If $hGUI = -1 Then
		Return MsgBox($MB_YESNO+$MB_DEFBUTTON2+$MB_TOPMOST,$sTitle,$sText,$nTimeout) = $IDYES
	Else
		Return MsgBox($MB_YESNO+$MB_DEFBUTTON2+$MB_TOPMOST,$sTitle,$sText,$nTimeout,$hGUI) = $IDYES
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Ok
; Description....: Ok message with an Ok button
; Syntax.........: _Ok($sText [,$sTitle = "" [,$nTimeout = 0 [,$hGUI = -1]]])
; Parameters.....: $sText					- "Ok"-message
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $nTimeout				- Time out in seconds
;                  $hGUI					- Window GUI handle
; Return values..: True (_Ok() can be used in any If or While)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Ok($sText = "",$sTitle = "",$nTimeOut = 0,$hGUI = -1)
	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sText) = 0 Then $sText = "Ok"
	If $hGUI = -1 Then
		MsgBox($MB_OK+$MB_TOPMOST,$sTitle,$sText,$nTimeout)
	Else
		MsgBox($MB_OK+$MB_TOPMOST,$sTitle,$sText,$nTimeout,$hGUI)
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Try
; Description....: Try dialogue with a Try and a Cancel button
; Syntax.........: _Try($sText [,$sTitle = "" [,$nTimeout = 0 [,$hGUI = -1]]])
; Parameters.....: $sText					- "Try"-message
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $nTimeout				- Time out in seconds
;                  $hGUI					- Window GUI handle
; Return values..: True = Retry, False = Cancel
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Try($sText = "",$sTitle = "",$nTimeOut = 0,$hGUI = -1)
	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sText) = 0 Then $sText = "Retry?"
	If $hGUI = -1 Then
		Return MsgBox($MB_RETRYCANCEL+$MB_DEFBUTTON2+$MB_TOPMOST,$sTitle,$sText,$nTimeout) = $IDRETRY
	Else
		Return MsgBox($MB_RETRYCANCEL+$MB_DEFBUTTON2+$MB_TOPMOST,$sTitle,$sText,$nTimeout,$hGUI) = $IDRETRY
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MessageBox
; Description....: Shows a message with maximum of 4 own buttons
; Syntax.........: _MessageBox($sMessage [,$sTitle = "" [,$iCloseButton = 0 [,$sButton1 = "" [,$sButton2 = "" [,$sButton3 = "" [,$sButton4 = "" [,$iIconHeight = 48 [, $iTextColor = -1, $iBackgroundColor = -1]]]]]]])
; Parameters.....: $sMessage				- Message of dialogue
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $iCloseButton			- When window close button is click, return this value, default 0
;                  $sButton1 - 4			- String on buttons 1 to 4, if empty button isn't shown
;                                             A tooltip can be given by inserting a | sign and adding text after it
;                  $iTextColor				- Text color, -1 = Windows theme
;                  $iBackgroundColor		- Background color, -1 = Windows theme
; Return values..: 0 = $iCloseButton, 1 = Button 1 pressed, 2 = Button 2 pressed, etc.
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MessageBox($sMessage,$sTitle = "",$iCloseButton = 0,$sButton1 = "",$sButton2 = "",$sButton3 = "",$sButton4 = "",$iTextColor = -1,$iBackgroundColor = -1)
	Const $iGUIWidth = 400,$iGUIHeight = 120,$iMargin = 15,$iButtonWidth = 90,$iButtonHeight = 25,$iControlHeight = 23
	Local $hGUI,$aMsg,$hLabel,$hButton1,$hButton2,$iButtonX,$hButton3,$hButton4,$sButtonTip1,$sButtonTip2,$sButtonTip3,$sButtonTip4,$iReturnValue

	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	$hGUI = GUICreate($sTitle,$iGUIWidth,$iGUIHeight,-1,-1,$WS_CAPTION+$WS_POPUP+$WS_SYSMENU,$WS_EX_TOPMOST)
	If $iBackgroundColor > -1 Then GUISetBkColor($iBackgroundColor)
	$hLabel = GUICtrlCreateLabel($sMessage,$iMargin,$iMargin,$iGUIWidth-2*$iMargin,$iGUIHeight-$iMargin-$iButtonHeight-$iMargin)
	If $iTextColor > -1 Then GUICtrlSetColor(-1,$iTextColor)
	If StringInStr($sButton1,"|") = 0 Then
		$sButtonTip1 = ""
	Else
		$sButtonTip1 = StringTrimLeft($sButton1,StringInStr($sButton1,"|"))
		$sButton1 = StringLeft($sButton1,StringInStr($sButton1,"|")-1)
	EndIf
	$hButton1 = GUICtrlCreateButton($sButton1,$iMargin,$iGUIHeight-$iMargin-$iButtonHeight,$iButtonWidth,$iButtonHeight)
	If StringLen($sButtonTip1) > 0 Then GUICtrlSetTip(-1,$sButtonTip1)
	If StringLen($sButton1) = 0 Then GUICtrlSetState(-1,$GUI_HIDE)
	If StringLen($sButton3) = 0 Then
		$iButtonX = $iGUIWidth-$iButtonWidth-$iMargin
	ElseIf StringLen($sButton4) > 0 Then
		$iButtonX = $iMargin+$iButtonWidth+3
	Else
		$iButtonX = ($iGUIWidth-$iButtonWidth)/2
	EndIf
	If StringInStr($sButton2,"|") = 0 Then
		$sButtonTip2 = ""
	Else
		$sButtonTip2 = StringTrimLeft($sButton2,StringInStr($sButton2,"|"))
		$sButton2 = StringLeft($sButton2,StringInStr($sButton2,"|")-1)
	EndIf
	$hButton2 = GUICtrlCreateButton($sButton2,$iButtonX,$iGUIHeight-$iMargin-$iButtonHeight,$iButtonWidth,$iButtonHeight)
	If StringLen($sButtonTip2) > 0 Then GUICtrlSetTip(-1,$sButtonTip2)
	If StringLen($sButton2) = 0 Then GUICtrlSetState(-1,$GUI_HIDE)
	$iButtonX = $iGUIWidth-$iButtonWidth-$iMargin
	If StringLen($sButton4) > 0 Then $iButtonX -= $iButtonWidth+3
	If StringInStr($sButton3,"|") = 0 Then
		$sButtonTip3 = ""
	Else
		$sButtonTip3 = StringTrimLeft($sButton3,StringInStr($sButton3,"|"))
		$sButton3 = StringLeft($sButton3,StringInStr($sButton3,"|")-1)
	EndIf
	$hButton3 = GUICtrlCreateButton($sButton3,$iButtonX,$iGUIHeight-$iMargin-$iButtonHeight,$iButtonWidth,$iButtonHeight)
	If StringLen($sButtonTip3) > 0 Then GUICtrlSetTip(-1,$sButtonTip3)
	If StringLen($sButton3) = 0 Then GUICtrlSetState(-1,$GUI_HIDE)
	If StringInStr($sButton4,"|") = 0 Then
		$sButtonTip4 = ""
	Else
		$sButtonTip4 = StringTrimLeft($sButton4,StringInStr($sButton4,"|"))
		$sButton4 = StringLeft($sButton4,StringInStr($sButton4,"|")-1)
	EndIf
	$hButton4 = GUICtrlCreateButton($sButton4,$iGUIWidth-$iButtonWidth-$iMargin,$iGUIHeight-$iMargin-$iButtonHeight,$iButtonWidth,$iButtonHeight)
	If StringLen($sButtonTip4) > 0 Then GUICtrlSetTip(-1,$sButtonTip4)
	If StringLen($sButton4) = 0 Then GUICtrlSetState(-1,$GUI_HIDE)

	GUISetState(@SW_SHOW)
	While 1
		$aMsg = GUIGetMsg(1)
		Switch $aMsg[0]
			Case $hButton1
				$iReturnValue = 1
				ExitLoop
			Case $hButton2
				$iReturnValue = 2
				ExitLoop
			Case $hButton3
				$iReturnValue = 3
				ExitLoop
			Case $hButton4
				$iReturnValue = 4
				ExitLoop
			Case $GUI_EVENT_CLOSE
				$iReturnValue = $iCloseButton
				ExitLoop
		EndSwitch
	WEnd
	GUIDelete($hGUI)
	Return $iReturnValue
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _HideMessage
; Description....: Shows a message which can be hidden, hide message checkbox state is returned
; Syntax.........: _HideMessage($sMessage [,$sTitle = "" [,$sHideText = "" [,$sOkButtonText = "Ok" [,$sFileIcon = "" [,$iIconPosition = 0 [,$iIconWidth = 48 [,$iIconHeight = 48 [, $iTextColor = -1 [, $iBackgroundColor = -1]]]]]]]]])
; Parameters.....: $sMessage				- Message of dialogue
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $sHideText				- Text of hide checkbox
;                  $sOkButtonText			- Text on Ok button
;                  $sFileIcon				- Icon to show, must be .ico file
;                  $iIconPosition			- 0 = left, 1 = right
;                  $iIconWidth				- Icon width, 0 = the default 48 pixels
;                  $iIconHeight				- Icon height, 0 = the default 48 pixels
;                  $iTextColor				- Text color, -1 = Windows theme
;                  $iBackgroundColor		- Background color, -1 = Windows theme
; Return values..: True = User wants to hide message, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _HideMessage($sMessage,$sTitle = "",$sHideText = "",$sOkButtonText = "Ok",$sFileIcon = "",$iIconPosition = 0,$iIconWidth = 48,$iIconHeight = 48,$iTextColor = -1,$iBackgroundColor = -1)
	Local $bHideCheck = False
	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sHideText) = 0 Then $sHideText = "Hide this message?"
	_Message($sMessage,$bHideCheck,$sTitle,$sHideText,$sOkButtonText,$sFileIcon,$iIconPosition,$iIconWidth,$iIconHeight,$iTextColor,$iBackgroundColor)
	Return $bHideCheck
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Message
; Description....: Shows a message which can be hidden, hide message checkbox state by passing reference to variable
; Syntax.........: _Message($sMessage, ByRef $HideCheck [,$sTitle = "" [,$sHideText = "" [,$sOkButtonText = "Ok" [,$sFileIcon = "" [,$iIconPosition = 0 [,$iIconWidth = 48 [,$iIconHeight = 48 [, $iTextColor = -1 [, $iBackgroundColor = -1]]]]]]]]])
; Parameters.....: $sMessage				- Message of dialogue
;                  $bHideCheck				- Boolean variable passed by reference for user to be able to hide message
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $sHideText				- Text of hide checkbox
;                  $sOkButtonText			- Text on Ok button
;                  $sFileIcon				- Icon to show, must be .ico file
;                  $iIconPosition			- 0 = left, 1 = right
;                  $iIconWidth				- Icon width, 0 = the default 48 pixels
;                  $iIconHeight				- Icon height, 0 = the default 48 pixels
;                  $iTextColor				- Text color, -1 = Windows theme
;                  $iBackgroundColor		- Background color, -1 = Windows theme
; Return values..: True = Closed by window close button
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Message($sMessage, ByRef $bHideCheck,$sTitle = "",$sHideText = "",$sOkButtonText = "Ok",$sFileIcon = "",$iIconPosition = 0,$iIconWidth = 48,$iIconHeight = 48,$iTextColor = -1,$iBackgroundColor = -1)
	Const $iGUIWidth = 460,$iGUIHeight = 150,$iMargin = 15,$iIconMargin = 10,$iButtonWidth = 100,$iButtonHeight = 25,$iControlHeight = 23
	Local $hGUI,$aMsg,$hLabel,$hHide,$hHideLabel,$hOk,$bClosed = False

	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sHideText) = 0 Then $sHideText = "Hide this message?"
	$hGUI = GUICreate($sTitle,$iGUIWidth,$iGUIHeight,-1,-1,$WS_CAPTION+$WS_POPUP+$WS_SYSMENU,$WS_EX_TOPMOST)
	If $iBackgroundColor > -1 Then GUISetBkColor($iBackgroundColor)
	If StringLen($sFileIcon) > 0 Then
		$iIconWidth = $iIconWidth <= 0 ? 48 : $iIconWidth
		$iIconHeight = $iIconHeight <= 0 ? 48 : $iIconHeight
		Switch $iIconPosition
			Case 0
				GUICtrlCreateIcon($sFileIcon,-1,$iIconMargin,$iIconMargin,$iIconWidth,$iIconHeight)
				$hLabel = GUICtrlCreateLabel($sMessage,$iIconMargin+$iIconWidth+$iMargin,$iMargin,$iGUIWidth-2*$iMargin-$iIconMargin-$iIconWidth,$iGUIHeight-$iMargin-$iButtonHeight-$iMargin)
				If $iTextColor > -1 Then GUICtrlSetColor(-1,$iTextColor)
			Case 1
				GUICtrlCreateIcon($sFileIcon,-1,$iGUIWidth-$iIconMargin-$iIconWidth,$iIconMargin,$iIconWidth,$iIconHeight)
				$hLabel = GUICtrlCreateLabel($sMessage,$iMargin,$iMargin,$iGUIWidth-2*$iMargin-$iIconMargin-$iIconWidth,$iGUIHeight-$iMargin-$iButtonHeight-$iMargin)
				If $iTextColor > -1 Then GUICtrlSetColor(-1,$iTextColor)
		EndSwitch
	Else
		$hLabel = GUICtrlCreateLabel($sMessage,$iMargin,$iMargin,$iGUIWidth-2*$iMargin,$iGUIHeight-$iMargin-$iButtonHeight-$iMargin)
		If $iTextColor > -1 Then GUICtrlSetColor(-1,$iTextColor)
	EndIf
	$hHide = GUICtrlCreateCheckbox("",$iMargin,$iGUIHeight-$iMargin-$iControlHeight,16,16)
	If $bHideCheck Then GUICtrlSetState(-1,$GUI_CHECKED)
	$hHideLabel = GUICtrlCreateLabel($sHideText,$iMargin+17,$iGUIHeight-$iMargin-$iControlHeight,$iGUIWidth-$iMargin-17-$iButtonWidth-$iMargin)
	If $iTextColor > -1 Then GUICtrlSetColor(-1,$iTextColor)
	$hOk = GUICtrlCreateButton($sOkButtonText,$iGUIWidth-$iButtonWidth-$iMargin,$iGUIHeight-$iMargin-$iButtonHeight,$iButtonWidth,$iButtonHeight)

	GUISetState(@SW_SHOW)
	While 1
		$aMsg = GUIGetMsg(1)
		Switch $aMsg[0]
			Case $hHideLabel
				GUICtrlSetState($hHide,GUICtrlRead($hHide) = $GUI_CHECKED ? $GUI_UNCHECKED : $GUI_CHECKED)
			Case $GUI_EVENT_CLOSE,$hOk
				$bHideCheck = GUICtrlRead($hHide) = $GUI_CHECKED
				$bClosed = $aMsg[0] = $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd
	GUIDelete($hGUI)
	Return $bClosed
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _DownloadFile
; Description....: Download file dialogue, downloads a file from given url
; Syntax.........: _DownloadFile($sURL [,$sFile = @MyDocumentsDir & "\DownloadedFile" [,$sTitle = "" [,$sDownloadLabel	= "")
; Parameters.....: $sURL					- File Url to download from
;                  $sFile					- File to download to
;                  $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;           	   $sDownloadLabel			- Label text under download progress bar
; Return values..: True = Downloaded successful, False = Aborted or download error
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _DownloadFile($sURL,$sFile = @MyDocumentsDir & "\DownloadedFile",$sTitle = "",$sDownloadLabel = "")
	Const $iGUIDownloadWidth = 300
	Local $iGUIDownloadHeight = 30,$hGUIDownload,$hDownloadBar,$hDownloadPercentage,$hDownload,$bDownloaded

	If StringLen($sFile) = 0 Then $sFile = @MyDocumentsDir & "\DownloadedFile"
	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	If StringLen($sDownloadLabel) > 0 Then $iGUIDownloadHeight += 18
	$hGUIDownload = GUICreate($sTitle,$iGUIDownloadWidth,$iGUIDownloadHeight)
	$hDownloadBar = GUICtrlCreateProgress(5,5,$iGUIDownloadWidth-35,20)
	$hDownloadPercentage = GUICtrlCreateLabel("0%",$iGUIDownloadWidth-28,8,50,20)
	If StringLen($sDownloadLabel) > 0 Then GUICtrlCreateLabel($sDownloadLabel,5,30,$iGUIDownloadWidth-10,20)
	$hDownload = InetGet($sURL,$sFile,$INET_FORCERELOAD+$INET_FORCEBYPASS,$INET_DOWNLOADBACKGROUND)
	GUISetState(@SW_SHOW,$hGUIDownload)
	$bDownloaded = False
	While 1
		$aMsg = GUIGetMsg(1)
		If InetGetInfo($hDownload,$INET_DOWNLOADERROR) <> 0 Then
			ExitLoop
		ElseIf InetGetInfo($hDownload,$INET_DOWNLOADCOMPLETE) Then
			If InetGetInfo($hDownload,$INET_DOWNLOADREAD) = FileGetSize($sFile) Then $bDownloaded = True
			ExitLoop
		Else
			If InetGetInfo($hDownload,$INET_DOWNLOADSIZE) <> 0 Then GUICtrlSetData($hDownloadBar,100*InetGetInfo($hDownload,$INET_DOWNLOADREAD)/InetGetInfo($hDownload,$INET_DOWNLOADSIZE))
			If InetGetInfo($hDownload,$INET_DOWNLOADREAD) > 0 Then GUICtrlSetData($hDownloadPercentage,Round(100*InetGetInfo($hDownload,$INET_DOWNLOADREAD)/InetGetInfo($hDownload,$INET_DOWNLOADSIZE)) & "%")
		EndIf
		Switch ($aMsg[0])
			Case $GUI_EVENT_CLOSE
				ExitLoop
		EndSwitch
	WEnd
	InetClose($hDownload)
	GUIDelete($hGUIDownload)
	Return $bDownloaded
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _OpenAudioPanel
; Description....: Opens the Windows audio panel with the play back and record devices
; Syntax.........: _OpenAudioPanel()
; Parameters.....: None
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _OpenAudioPanel()
	Const $sMicrosoftSound="Microsoft.Sound",$sGUID_MicrosoftSound="{F2DDFC82-8F12-4CDD-B7DC-D4FE1425AA4D}"
	Const $sCLSID_OpenControlPanel = "{06622D85-6856-4460-8DE1-A81921B41C4B}",$sIID_IOpenControlPanel = "{D11AD862-66DE-4DF4-BF6C-1F5621996AF1}"
	Const $sTagIOpenControlPanel= "Open hresult(wstr;wstr;ptr);GetPath hresult(wstr;wstr;uint);GetCurrentView hresult(int*)"
	Local $oOpenControlPanel = ObjCreateInterface($sCLSID_OpenControlPanel,$sIID_IOpenControlPanel,$sTagIOpenControlPanel)

	$oOpenControlPanel.Open($sMicrosoftSound,"",Null)	; playback devices tab
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarType
; Description....: Sets progress bar type to Windows default or colored rectangle with moving percentage
; Syntax.........: _ProgressBarType($iType = 0 [,$iBarColor = 0x0078d7 [,$iPercentageColor = -1 [,$iBackgroundColor = -1]]])
; Parameters.....: $iType					- Progress bar type, 0 = Windows default, 1 = colored rectangle with moving percentage label
;                  $iBarColor				- Progress bar color, default Windows 10 blue, -1 = default color
;                  $iPercentageColor		- Percentage label color, -1 = white
;                  $iBackgroundColor		- Background color of progress bar rectangle
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarType($iType = 0,$iBarColor = 0x0078d7,$iPercentageColor = -1,$iBackgroundColor = -1)
	$iType = ($iType < 0) ? (0) : ($iType)
	$iType = ($iType > 1) ? (1) : ($iType)
	Assign("_iProgressBarType",$iType,$ASSIGN_FORCEGLOBAL)
	Assign("_iProgressBarColor",($iBarColor = -1) ? 0x0078d7 : $iBarColor,$ASSIGN_FORCEGLOBAL)
	Assign("_iProgressBarPercentageColor",$iPercentageColor,$ASSIGN_FORCEGLOBAL)
	Assign("_iProgressBarBackgroundColor",$iBackgroundColor,$ASSIGN_FORCEGLOBAL)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarCreate
; Description....: Creates a sizable horizontal or vertical progress bar (which must be destroyed by _ProgressBarDestroy)
; Syntax.........: _ProgressBarCreate([$sTitle = "" [,$sLabel = "" [,$bPercentageLabel = True [,$bOrientation = True [,$iTop = -1 [,$iLeft = -1 [,$iWidthHeight = 300]]]]]]]])
; Parameters.....: $sTitle					- Title of dialogue, if empty title set by _DialogueTitle() is used
;                  $sLabel					- Label under progress bar
;                  $bPercentageLabel		- True = Show percentage label, False = Not
;                  $bOrientation			- True = Horizontal, False = Vertical
;                  $iTop					- Top coordinate of progess bar, -1 = center
;                  $iLeft					- Left coordinate, -1 = center
;                  $iWidthHeight			- Progress bar window width or height
; Return values..: True = Progress bar created, False = Progress bar already active
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarCreate($sTitle = "",$sLabel = "",$bPercentageLabel = True,$bOrientation = True,$iTop = -1,$iLeft = -1,$iWidthHeight = 300)
	If _ProgressBarActive() Then Return False
	Global $_hProgressBarControl,$_bProgressBarOrientation,$_iProgressBarWidthHeight,$_nProgressBarValue,$_hProgressPercentageControl
	Local $hGUI,$iGUIWidth,$iGUIHeight

	If StringLen($sTitle) = 0 And IsDeclared("_sDialogueTitle") = $DECLARED_GLOBAL Then $sTitle = Eval("_sDialogueTitle")
	$_bProgressBarOrientation = $bOrientation
	If $bOrientation Then		; horizontal progress bar
		$iGUIWidth = $iWidthHeight
		$iGUIHeight = 30
		If StringLen($sLabel) > 0 Then $iGUIHeight += 18
		$hGUI = GUICreate($sTitle,$iGUIWidth,$iGUIHeight,$iTop,$iLeft,$WS_CAPTION+$WS_POPUP,$WS_EX_TOPMOST)
		If IsDeclared("_iProgressBarType") = $DECLARED_GLOBAL And Eval("_iProgressBarType") = 1 Then
			$_iProgressBarWidthHeight = $iGUIWidth-10
			If IsDeclared("_iProgressBarBackgroundColor") = $DECLARED_GLOBAL And Eval("_iProgressBarBackgroundColor") > -1 Then
				GUICtrlCreateLabel("",5,5,$_iProgressBarWidthHeight,20)
				GUICtrlSetBkColor(-1,Eval("_iProgressBarBackgroundColor"))
			EndIf
			$_hProgressBarControl = GUICtrlCreateLabel("",5,5,0,20)
			If IsDeclared("_iProgressBarColor") = $DECLARED_GLOBAL Then GUICtrlSetBkColor(-1,Eval("_iProgressBarColor"))
			$_hProgressPercentageControl = GUICtrlCreateLabel("0%",0,8,50,20)
			GUICtrlSetBkColor(-1,$GUI_BKCOLOR_TRANSPARENT)
			GUICtrlSetColor(-1,0xFFFFFF)
			If IsDeclared("_iProgressBarPercentageColor") = $DECLARED_GLOBAL And Eval("_iProgressBarPercentageColor") > -1 Then GUICtrlSetColor(-1,Eval("_iProgressBarPercentageColor"))
		Else
			$_iProgressBarWidthHeight = ($bPercentageLabel) ? ($iGUIWidth-35) : ($iGUIWidth-10)
			$_hProgressBarControl = GUICtrlCreateProgress(5,5,$_iProgressBarWidthHeight,20)
			$_hProgressPercentageControl = GUICtrlCreateLabel("0%",$iGUIWidth-28,9,50,20)
			If IsDeclared("_iProgressBarPercentageColor") = $DECLARED_GLOBAL And Eval("_iProgressBarPercentageColor") > -1 Then GUICtrlSetColor(-1,Eval("_iProgressBarPercentageColor"))
		EndIf
		If Not $bPercentageLabel Then GUICtrlSetState(-1,$GUI_HIDE)
		If StringLen($sLabel) > 0 Then GUICtrlCreateLabel($sLabel,5,30,$iGUIWidth-10,20)
	Else
		$iGUIWidth = 32
		$iGUIHeight = $iWidthHeight
		If StringLen($sLabel) > 0 Then $iGUIHeight += 18
		$hGUI = GUICreate($sTitle,$iGUIWidth,$iGUIHeight,$iTop,$iLeft,$WS_CAPTION+$WS_POPUP,$WS_EX_TOPMOST)
		If IsDeclared("_iProgressBarType") = $DECLARED_GLOBAL And Eval("_iProgressBarType") = 1 Then
			$_iProgressBarWidthHeight = (StringLen($sLabel) > 0) ? ($iGUIHeight-28) : ($iGUIHeight-10)
			If IsDeclared("_iProgressBarBackgroundColor") = $DECLARED_GLOBAL And Eval("_iProgressBarBackgroundColor") > -1 Then
				GUICtrlCreateLabel("",3,5,26,$_iProgressBarWidthHeight)
				GUICtrlSetBkColor(-1,Eval("_iProgressBarBackgroundColor"))
			EndIf
			$_hProgressBarControl = GUICtrlCreateLabel("",2,5,26,10)
			If IsDeclared("_iProgressBarColor") = $DECLARED_GLOBAL Then GUICtrlSetBkColor(-1,Eval("_iProgressBarColor"))
			$_hProgressPercentageControl = GUICtrlCreateLabel("0%",0,3,5,20)
			GUICtrlSetBkColor(-1,$GUI_BKCOLOR_TRANSPARENT)
			GUICtrlSetColor(-1,0xFFFFFF)
			If IsDeclared("_iProgressBarPercentageColor") = $DECLARED_GLOBAL And Eval("_iProgressBarPercentageColor") > -1 Then GUICtrlSetColor(-1,Eval("_iProgressBarPercentageColor"))
		Else
			If $bPercentageLabel Then
				$_iProgressBarWidthHeight = (StringLen($sLabel) > 0) ? ($iGUIHeight-48) : ($iGUIHeight-30)
			Else
				$_iProgressBarWidthHeight = (StringLen($sLabel) > 0) ? ($iGUIHeight-28) : ($iGUIHeight-10)
			EndIf
			$_hProgressBarControl = GUICtrlCreateProgress(5,($bPercentageLabel) ? (25) : (5),20,$_iProgressBarWidthHeight,$PBS_VERTICAL)
			$_hProgressPercentageControl = GUICtrlCreateLabel("0%",5,5,45,20)
		EndIf
		If Not $bPercentageLabel Then GUICtrlSetState(-1,$GUI_HIDE)
		If StringLen($sLabel) > 0 Then GUICtrlCreateLabel($sLabel,5,$iGUIHeight-18,$iGUIWidth-5,20)
	EndIf
	$_nProgressBarValue = 0
	GUISetState(@SW_SHOW,$hGUI)
	Assign("_hProgressBarGUI",$hGUI,$ASSIGN_FORCEGLOBAL)
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarActive
; Description....: Returns if progress bar is currently active
; Syntax.........: _ProgressBarActive()
; Parameters.....: None
; Return values..: True = Currently active, False = Inactive
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarActive()
	Return IsDeclared("_hProgressBarGUI") = $DECLARED_GLOBAL And Eval("_hProgressBarGUI") <> 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarSet
; Description....: Sets progress bar percentage
; Syntax.........: _ProgressBarSet([$nPercentage = 0])
; Parameters.....: $nPercentage				- Percentage to set
; Return values..: True = Percentage set, False = Progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarSet($nPercentage = 0)
	If Not _ProgressBarActive() Then Return False
	$_nProgressBarValue = $nPercentage
	__ProgressBarSet()
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarAdd
; Description....: Adds a percentage to progress bar
; Syntax.........: _ProgressBarAdd([$nPercentage = 1])
; Parameters.....: $nPercentage				- Percentage to add, may be negative
; Return values..: True = Percentage added, False = Progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarAdd($nPercentage = 1)
	If Not _ProgressBarActive() Then Return False
	$_nProgressBarValue += $nPercentage
	__ProgressBarSet()
	Return True
EndFunc

; #INTERNAL_USE_ONLY# ====================================================================================================================
; Name...........: __ProgressBarSet
; Description....: Internal function to set bar
; Parameters.....: None
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func __ProgressBarSet()
	$_nProgressBarValue = ($_nProgressBarValue > 100) ? (100) : ($_nProgressBarValue)
	$_nProgressBarValue = ($_nProgressBarValue < 0) ? 0 : ($_nProgressBarValue)
	If IsDeclared("_iProgressBarType") = $DECLARED_GLOBAL And Eval("_iProgressBarType") = 1 Then
		If $_bProgressBarOrientation Then		; horizontal progress bar
			If $_nProgressBarValue > 0 Then GUICtrlSetPos($_hProgressBarControl,5,5,$_iProgressBarWidthHeight*($_nProgressBarValue/100),20)
			GUICtrlSetPos($_hProgressPercentageControl,$_iProgressBarWidthHeight*($_nProgressBarValue/200),8)
		Else
			If $_nProgressBarValue > 0 Then GUICtrlSetPos($_hProgressBarControl,3,5+$_iProgressBarWidthHeight*(1-$_nProgressBarValue/100),26,$_iProgressBarWidthHeight*($_nProgressBarValue/100))
			Local $iControlX = ($_nProgressBarValue = 100) ? 3 : 5
			GUICtrlSetPos($_hProgressPercentageControl,$iControlX,5+$_iProgressBarWidthHeight*(1-$_nProgressBarValue/200),30)
		EndIf
	Else
		GUICtrlSetData($_hProgressBarControl,$_nProgressBarValue)
		If Not $_bProgressBarOrientation Then
			Local $iControlX = ($_nProgressBarValue = 100) ? 3 : 5
			GUICtrlSetPos($_hProgressPercentageControl,$iControlX,5,45,20)
		EndIf
	EndIf
	GUICtrlSetData($_hProgressPercentageControl,Round($_nProgressBarValue) & "%")
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarGet
; Description....: Gets progress bar percentage
; Syntax.........: _ProgressBarGet()
; Parameters.....: None
; Return values..: Progress bar percentage, 0 = 0 percentage or progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarGet()
	If Not _ProgressBarActive() Then Return 0
	Return $_nProgressBarValue
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarFull
; Description....: Tests if progress bar is 100%
; Syntax.........: _ProgressBarFull()
; Parameters.....: None
; Return values..: True = Full, $False = Not or progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarFull()
	If Not _ProgressBarActive() Then Return False
	Return _ProgressBarGet() >= 100
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarEmpty
; Description....: Tests if progress bar is 0%
; Syntax.........: _ProgressBarEmpty()
; Parameters.....: None
; Return values..: True = Empty, $False = Not or progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarEmpty()
	If Not _ProgressBarActive() Then Return False
	Return _ProgressBarGet() <= 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ProgressBarDestroy
; Description....: Destroys progress bar GUI after some waiting (to show completion)
; Syntax.........: _ProgressBarDestroy([$iWaitFor = 1000])
; Parameters.....: $iWaitFor				- Milliseconds to show completion
; Return values..: True = Progress bar destroyed, False = Progress bar inactive (not created)
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ProgressBarDestroy($iWaitFor = 1000)
	If Not _ProgressBarActive() Then Return False
	Sleep($iWaitFor)
	GUIDelete(Eval("_hProgressBarGUI"))
	Assign("_hProgressBarGUI",0,$ASSIGN_FORCEGLOBAL)
	$_hProgressBarControl = 0
	$_nProgressBarValue = 0
	$_bProgressBarOrientation = True
	$_iProgressBarWidthHeight = 0
	$_hProgressPercentageControl = 0
	Return True
EndFunc