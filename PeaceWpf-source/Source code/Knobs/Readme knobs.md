# AutoIt switches and dial knobs system

Copyright P.E. Verbeek, version 1.17

For this system to work all you need is to include Knobs.au3 and add (a folder with) images for the switches and rotary knobs.  
Example images are located in the Images folder.  
Note: All images are **90 dpi**. As gdi+ only rotates image of 90 dpi correctly so the following must be 90 dpi:

*knobdialback.png* and *knobdialbackdisabled.png*

*knobdialfront.png* and *knobdialfrontdisabled.png*

You can browse through *Peace.au3* to see how I use this system for the *Peace effects panel*.

**version history**  
1.17	Switches and rotary knobs drawing quality improved a little bit  
1.16	A label may show a converted value through %c and passing conversion rate in RotaryKnobCreate()  
1.15	Times of the showing of tooltips of switch and rotary knobs can be adjusted  
1.14	Bug: Double enabling a switch or rotary knob would redraw the background of the controlling label (transparency was gone?)  
1.13	For drawing of switch and rotary knobs the window background color is always retrieved instead of once  
1.12	RotaryKnobCreate(): the snap to value parameter can be an array of possible values  
1.11	Support added for more than 1 GUI  
	SwitchKnobsAddGUI() and RotaryKnobsAddGUI() to add another GUI for switch or rotary knobs  
	SwitchKnobsDraw() and RotaryKnobsDraw() changed: parameter added for GUI number (0 = first, 1 = second, etc.)  
	SwitchKnobsGetGUI() and RotaryKnobsGetGUI() to get GUI handle by GUI number (0 = first, 1 = second, etc.)  
	SwitchKnobsGetGraphic() and RotaryKnobsGetGraphic() to get GDI plus graphic handle by GUI number (0 = first, 1 = second, etc.)  
	SwitchKnobGetGUI() and RotaryKnobGetGUI(): deprecated because wrong function name (see above)  
	SwitchKnobsIsGUI() and RotaryKnobsIsGUI() to check if given GUI is a switch or rotary knobs GUI  
	SwitchKnobsResetAll() and RotaryKnobsResetAll() changed: parameter added for GUI number (-1 = all GUIs, 0 = first, 1 = second, etc.)  
	Bug: Test background wasn't drawn on bottom label for rotary knobs  
	Bug: SwitchKnobEnable() and RotaryKnobEnable() didn't consider none used labels  
1.10	SwitchKnobsLabelsDisabledColor() and SwitchKnobsLabelsColor() to color labels  
	RotaryKnobsLabelsDisabledColor() and RotaryKnobsLabelsColor() to color labels  
1.09	SwitchKnobsTest() and RotaryKnobsTest() accept a color for label showing  
1.08	SwitchKnobsLabelsDisabled() and RotaryKnobsLabelsDisabled() to show labels as disabled font or normal font  
1.07	Clearing area of switch and rotary knob increased by 1 pixel on each side, needed for erasing any pixels of previous drawn knob  
1.06	Local variable removed which isn't needed for a For loop  
1.05	RotaryKnobsOnOff(): new argument for off mechanisme and image  
	RotaryKnobSwitch() to switch knob on/off  
	RotaryKnobToggle() to toggle knob on/off  
	RotaryKnobOn() to get if knob is switched off  
	RotaryKnobIncreaseValue() and RotaryKnobDecreaseValue(): 2 arguments for checking on (other than default value) state and off state  
1.04	Draw system improved: knobs are only redrawn if necessary  
	Draw system improved: knobs are correctly redrawn at a window resize  
	SwitchKnobShow() and RotaryKnobShow() added to show or hide the knob  
	SwitchKnobEnabled() added  
	SwitchKnobEnable() and RotaryKnobEnable() perform a redraw. Own redraw no longer required  
	SwitchKnobsScanSize() and RotaryKnobsScanSize() added to enlarge (or decrease) scan surface size  
	Controls fixated on their positions when window resizing. This behaviour can initially be set to false  
1.03	Width of bottom switch label enlarged to width main label  
1.02	More comments added  
1.01	Initial release version

