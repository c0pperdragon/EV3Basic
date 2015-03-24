using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SmallBasic.Library;
using EV3Communication;

namespace SmallBasicEV3Extension
{

    /// <summary>
    /// Control the LCD display on the brick.
    /// The EV3 has a black-and-white display with 178x128 pixels. All pixels are addressed with X,Y coordinates where X=0 is the left edge and Y=0 is the top edge.
    /// </summary>
    [SmallBasicType]
    public static class LCD
    {
        private static ByteCodeBuffer commandBuffer = new ByteCodeBuffer();
        private static bool autoflush = true;

        // must be called while holding a lock on commandBuffer
        private static void CheckFlush()
        {
            if ( (commandBuffer.Length>0 && autoflush)
            ||   (commandBuffer.Length>700) )                   // prevent to exceed 1024 bytes limit for usb communication
            {
                // add a check that the program is still running
                commandBuffer.OP(0x0C);            // opProgram_Info
                commandBuffer.CONST(0x16);         //  CMD: GET_STATUS = 0x16
                commandBuffer.CONST(1);            //  program slot 1 = user slot
                commandBuffer.GLOBVAR(0);          //  result -> when this reseives 0x40, the program was stopped
                commandBuffer.OP(0x6C);            // opJR_EQ8
                commandBuffer.GLOBVAR(0);           // running state in in global variable 0
                commandBuffer.CONST(0x40);          // check if program was stopped
                commandBuffer.CONST(2);             // jump over next 2 opocodes
                
                // trigger the update
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x00);        // CMD: UPDATE = 0x00

                commandBuffer.OP(0x01);            // NOP  - need this as branch target

                EV3Communicator.DirectCommand(commandBuffer, 1, 0);

                commandBuffer.Clear();
            }
        }


        /// <summary>
        /// Memorize all subsequent changes to the display instead of directly drawing them. 
        /// At the next call to Update(), these changes will be finally drawn. 
        /// You can use this feature to prevent flickering or to speed up drawing complex things to the LCD.
        /// </summary>
        public static void StopUpdate()
        {
            lock (commandBuffer)
            {
                autoflush = false;
            }
        }

        /// <summary>
        /// Draw all changes to the display that have happened since the last call to StopUpdate().
        /// After Update() everthing will again be drawn directly unless you use the StopUpdate() once more.
        /// </summary>
        public static void Update()
        {
            lock (commandBuffer)
            {
                autoflush = true;
                CheckFlush();
            }
        }

        /// <summary>
        /// Set all pixels of the display to white.
        /// </summary>
        public static void Clear()
        {
            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x12);        // CMD: TOPLINE = 0x12
                commandBuffer.CONST(0);
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.OP(0x01);           // CMD: CLEAN = 0x01
                CheckFlush();
            }
        }

        /// <summary>
        /// Set a single pixel on the display to a color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void Pixel(Primitive color, Primitive x, Primitive y)
        {
            int _x, _y, _color;
            Int32.TryParse(color==null ? "":color.ToString(), out _color);
            Int32.TryParse(x==null ? "":x.ToString(), out _x);
            Int32.TryParse(y==null ? "":y.ToString(), out _y);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x02);        // CMD: PIXEL = 0x02
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                CheckFlush();
            }
        }

        /// <summary>
        /// Set a straight line of pixels to a color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x1">X coordinate of start point</param>
        /// <param name="y1">Y coordinate of start point</param>
        /// <param name="x2">X coordinate of end point</param>
        /// <param name="y2">Y coordinate of end point</param>
        public static void Line(Primitive color, Primitive x1, Primitive y1, Primitive x2, Primitive y2)
        {
            int _x1, _y1, _x2, _y2, _color;
            Int32.TryParse(color==null ? "":color.ToString(), out _color);
            Int32.TryParse(x1==null ? "":x1.ToString(), out _x1);
            Int32.TryParse(y1==null ? "":y1.ToString(), out _y1);
            Int32.TryParse(x2==null ? "":x2.ToString(), out _x2);
            Int32.TryParse(y2==null ? "":y2.ToString(), out _y2);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x03);        // LINE = 0x03
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x1);
                commandBuffer.CONST(_y1);
                commandBuffer.CONST(_x2);
                commandBuffer.CONST(_y2);
                CheckFlush();
            }
        }

        /// <summary>
        /// Draws a circle in the given color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">X coordinate of center point</param>
        /// <param name="y">Y coordinate of center point</param>
        /// <param name="radius">Radius of the circle</param>
        public static void Circle(Primitive color, Primitive x, Primitive y, Primitive radius)
        {
            int _x, _y, _radius, _color;
            Int32.TryParse(color==null ? "":color.ToString(), out _color);
            Int32.TryParse(x==null ? "":x.ToString(), out _x);
            Int32.TryParse(y==null ? "":y.ToString(), out _y);
            Int32.TryParse(radius==null ? "":radius.ToString(), out _radius);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x04);        // CIRCLE = 0x04
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.CONST(_radius);
                CheckFlush();
            }
        }

        /// <summary>
        /// Write a given text (or number) in a color to the display
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">X coordinate where text starts</param>
        /// <param name="y">Y coordinate of the top corner</param>
        /// <param name="font">Size of the letters: 0 (TINY), 1 (SMALL), 2 (BIG)</param>
        /// <param name="text">The text (or number) to write to the display</param>
        public static void Text(Primitive color, Primitive x, Primitive y, Primitive font, Primitive text)
        {
            int _x, _y, _font, _color;
            String _text;
            Int32.TryParse(color==null?"":color.ToString(), out _color);
            Int32.TryParse(x==null?"":x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);
            Int32.TryParse(font == null ? "":font.ToString(), out _font);
            _text = text.ToString();

            if (_text.Length>100)        // limit text line sizes to sensible value
            {
                _text = _text.Substring(0, 100);            
            }

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x11);        // CMD: SELECT_FONT = 0x11
                commandBuffer.CONST(_font);
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x05);        // TEXT = 0x05
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.STRING(_text);
                CheckFlush();
            }
        }

        /// <summary>
        /// Write a given text (or number) in black color to the display.
        /// When you need more control over the visual appearance, use the function 'Text' instead.
        /// </summary>
        /// <param name="x">X coordinate where text starts</param>
        /// <param name="y">Y coordinate of the top corner</param>
        /// <param name="text">The text (or number) to write to the display</param>
        public static void Write(Primitive x, Primitive y, Primitive text)
        {
            Text(new Primitive(1), x, y, new Primitive(1), text);
        }

        /// <summary>
        /// Fill a rectangle with a color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">Left edge of rectangle</param>
        /// <param name="y">Top edge of rectangle</param>
        /// <param name="width">Width of rectangle</param>
        /// <param name="height">Height of rectangle</param>
        public static void FillRect(Primitive color, Primitive x, Primitive y, Primitive width, Primitive height)
        {
            int _x, _y, _width, _height, _color;
            Int32.TryParse(color==null?"":color.ToString(), out _color);
            Int32.TryParse(x == null ? "" : x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);
            Int32.TryParse(width == null ? "" : width.ToString(), out _width);
            Int32.TryParse(height == null ? "" : height.ToString(), out _height);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x09);        // CMD: FILLRECT = 0x09
                commandBuffer.CONST(_color);      // color
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.CONST(_width);
                commandBuffer.CONST(_height);
                CheckFlush();
            }
        }

        /// <summary>
        /// Draw an outline of a rectangle with a color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">Left edge of rectangle</param>
        /// <param name="y">Top edge of rectangle</param>
        /// <param name="width">Width of rectangle</param>
        /// <param name="height">Height of rectangle</param>
        public static void Rect(Primitive color, Primitive x, Primitive y, Primitive width, Primitive height)
        {
            int _x, _y, _width, _height, _color;
            Int32.TryParse(color == null ? "" : color.ToString(), out _color);
            Int32.TryParse(x == null ? "" : x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);
            Int32.TryParse(width == null ? "" : width.ToString(), out _width);
            Int32.TryParse(height == null ? "" : height.ToString(), out _height);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x0A);        // CMD: RECT = 0x0A
                commandBuffer.CONST(_color);      // color
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.CONST(_width);
                commandBuffer.CONST(_height);
                CheckFlush();
            }
        }

        /// <summary>
        /// Invert the colors of all pixels inside of a rectangle
        /// </summary>
        /// <param name="x">Left edge of rectangle</param>
        /// <param name="y">Top edge of rectangle</param>
        /// <param name="width">Width of rectangle</param>
        /// <param name="height">Height of rectangle</param>
        public static void InverseRect(Primitive x, Primitive y, Primitive width, Primitive height)
        {
            int _x, _y, _width, _height;
            Int32.TryParse(x == null ? "" : x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);
            Int32.TryParse(width == null ? "" : width.ToString(), out _width);
            Int32.TryParse(height == null ? "" : height.ToString(), out _height);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x10);        // CMD: INVERSERECT = 0x10
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.CONST(_width);
                commandBuffer.CONST(_height);
                CheckFlush();
            }
        }

        /// <summary>
        /// Draws a filled circle with a given color.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">X coordinate of center point</param>
        /// <param name="y">Y coordinate of center point</param>
        /// <param name="radius">Radius of the circle</param>
        public static void FillCircle(Primitive color, Primitive x, Primitive y, Primitive radius)
        {
            int _x, _y, _radius, _color;
            Int32.TryParse(color == null ? "" : color.ToString(), out _color);
            Int32.TryParse(x == null ? "" : x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);
            Int32.TryParse(radius == null ? "" : radius.ToString(), out _radius);

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);           // UI_DRAW
                commandBuffer.CONST(0x18);        // CMD: FILLCIRCLE = 0x18
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.CONST(_radius);
                CheckFlush();
            }
        }

        /// <summary>
        /// Draw a bitmap file in a given color to the display.
        /// </summary>
        /// <param name="color">0 (white) or 1 (black)</param>
        /// <param name="x">X coordinate of left edge</param>
        /// <param name="y">Y coordinate of top edge</param>
        /// <param name="filename">Name of the file containing the bitmap</param>
        public static void BmpFile(Primitive color, Primitive x, Primitive y, Primitive filename)
        {
            int _x, _y, _color;
            Int32.TryParse(color == null ? "" : color.ToString(), out _color);
            Int32.TryParse(x == null ? "" : x.ToString(), out _x);
            Int32.TryParse(y == null ? "" : y.ToString(), out _y);

            String fname = (filename == null ? "" : filename.ToString());
            if (!fname.StartsWith("/"))   // relative path
            {   
                fname = "/home/root/lms2012/prjs/" + fname;
            }
            if (fname.Length > 500)
            {
                fname = fname.Substring(0, 500);
            }

            lock (commandBuffer)
            {
                commandBuffer.OP(0x84);     // UI_DRAW
                commandBuffer.CONST(0x1C);  // CMD: BMPFILE = 0x1C
                commandBuffer.CONST(_color);
                commandBuffer.CONST(_x);
                commandBuffer.CONST(_y);
                commandBuffer.STRING(fname);
                CheckFlush();
            }
        }
    }
}
