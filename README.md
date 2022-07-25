
# SignalPlus
More device support for SignalRGB

![](https://github.com/Trojaner/signal-plus/blob/master/Example.gif)

## Device Integrations

### Arduino Integration
Create your own ARGB controller for Neopixel, WS2801, WS2811, WS2812B, LPD8806, TM1809 and more controllable from PC or other devices supporting serial communication.

### Yeelight
Todo

### Nanoleaf
Todo

### Govee
Todo

## Integrating Arduino

### 1. Requirements
1. An Arduino board that supports high baud rates as high as 921.600. I use the Arduino Mega 2560 R3.  The ideal choice would be an Arduino Due with a level shifter as it does not suffer from [FastLED's interrupt issue](https://github.com/FastLED/FastLED/wiki/Interrupt-problems).
2. Any [FastLED compatible](https://github.com/FastLED/FastLED/wiki/Chipset-reference) LED matrix or strip. Up to 255 LEDs supported.
3. External +5V power source (if you have lots of LEDs) 

### 2. Connecting the LED Strip to the Arduino
#### Example for WS2812B LED Strips
![](https://i.imgur.com/1zlMvis.png)
*Reference WS2812B for PC pinout*

1. **LED Strip [D]** to **Arduino [PIN 3]**
2. **LED Strip [5V]** to **power source [5V]**
3. **LED Strip [GND]** to **power source [GND]**
4. **Arduino [GND]** to **power source [GND]**

### 3. Software preparations
1. Adjust the LED type and LED amount in the [firmware](https://github.com/Trojaner/signal-plus/tree/master/firmware/arduino/Firmware.ino) by changing `LED_TYPE` and `NUM_LEDS` to your needs.
2. Adjust the [C# client](https://github.com/Trojaner/arduino-argb/tree/master/csharp/) or write your own client program using your favorite language with the protocol below.
3. Done!

## Protocol
The protocol was designed with speed in mind. 
A [sample C# based implementation](https://github.com/Trojaner/signal-plus/tree/master/client/csharp/RgbDevice.cs) is available.

### Packet Structure

```
[Header]
[00] 0xFC (start marker)
[01] [command]
[02] [payload size]
[03] [header crc8]

[Payload]
[04] [first payload byte]
[05] [second payload byte]
...
[XX] [payload crc8]
```

Use 0xD5 as polynemial for your CRC8 algorithm.

### Commands
The following commands are available.

#### COMMAND_SET_LED (0x01) 
Sets a led to the given color.  
  
**Payload:**  
```
[00] [led_id]
[01] [R]
[02] [G]
[03] [B]
```

**Example Packet**:  
Set 6th led color to (0, 0, 255).  
`FC 01 04 E7 06 00 00 FF B2`

#### COMMAND_SET_LED_CHUNK (0x02) (experimental)
 Sets a chunk of leds for faster updates.  
   
**Payload:**  
```
[00] [start led]
[01] [end led]
[03] [R] // start led
[04] [G]
[05] [B]
[03] [R] // start + 1 led
[04] [G]
[05] [B]
...
[??] [R] // end led
[??] [G]
[??] [B]
```
**Example**: 
Set leds 0 - 10 to a rainbow gradient.  
`FC 02 21 75 00 0A BF 40 40 BF 8C 40 A6 BF 40 59 BF 40 40 BF 73 40 BF BF 40 73 BF 59 40 BF A6 40 BF BF 40 8C 00 2B`  
  
**Note**: Does not work correctly yet.

#### COMMAND_SET_BRIGHTNESS (0x02)
Set the brightness for the entire LED strip.  

**Payload:**  
```
[00] [led_id]
[01] [brightness]
```

**Example Packet**:  
Set brightness to 64.  
`FC 03 01 DA 40 9D`  
  
###  Limitations
Depending on your Arduino, many packets may get lost because of [FastLED's interrupt issue](https://github.com/FastLED/FastLED/wiki/Interrupt-problems). To counter this, use a high baud rate and keep "spamming" your current state instead of sending it only once.   

## To-Do
- [ ] Supporting more than 255 LEDs and multiple channels for Arduino
- [ ] Autostart & hiding console
- [ ] GUI

## License
[MIT](https://github.com/Trojaner/signal-plus/blob/master/LICENSE.txt)
