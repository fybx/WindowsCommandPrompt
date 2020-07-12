/*
 *       WindowsCommandPrompt version 1.1
 *       
 *         written by fybalaban @ 2020
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LuddeToolset;

namespace WindowsCommandPrompt
{
    /// <summary>
    /// Use a RichTextBox control to implement the Command Prompt in your application.
    /// </summary>
    public class CmdWrapperTextBox : IDisposable
    {
        #region Properties
        /// <summary>
        /// Returns the TextBox used to display output and receive input.
        /// </summary>
        public RichTextBox TextBox { get; }
        /// <summary>
        /// Returns true if Process is stopped and the connection the Command Prompt is shut.
        /// </summary>
        public bool Closed { get; private set; }
        /// <summary>
        /// Returns the position of caret in the TextBox.
        /// </summary>
        public int PositionOfCaret => TextBox.SelectionStart;
        /// <summary>
        /// Returns the position of '>' character when the prompt is awaiting input.
        /// </summary>
        public int PositionOfSplitter { get; private set; }
        /// <summary>
        /// Returns the Title property of the Command Prompt. Change it by "title [arg]" command.
        /// </summary>
        public string ConsoleTitle { get; private set; }
        #endregion

        #region Fields
        private bool EnteringText;
        private string Text => TextBox.Text;
        private int LengthOfInput = 0;
        private BackgroundWorker ErrorWorker = new BackgroundWorker();
        private BackgroundWorker OutputWorker = new BackgroundWorker();
        private Process CommandHost;
        private StreamWriter InputWriter;
        private TextReader OutputReader;
        private TextReader ErrorReader;
        #endregion

        /// <summary>
        /// Start the process and open the i/o connection between Command Prompt and TextBox.  
        /// </summary>
        /// <param name="textBox">RichTextBox to use for displaying output and receiving input</param>
        public CmdWrapperTextBox(RichTextBox textBox)
        {
            TextBox = textBox;
            TextBox.Clear();
            TextBox.TextChanged += this.OnTextChanged;
            TextBox.KeyDown += this.OnKeyDown;
            TextBox.KeyUp += this.OnKeyUp;
            TextBox.MouseClick += this.OnMouseClick;

            ConsoleTitle = @"Windows Command Prompt Wrapper by fybalaban";

            ProcessStartInfo info = new ProcessStartInfo(@"C:\Windows\System32\cmd.exe")
            {
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            //  Configure the output worker.
            OutputWorker.WorkerReportsProgress = true;
            OutputWorker.WorkerSupportsCancellation = true;
            OutputWorker.DoWork += OutputWorker_DoWork;

            //  Configure the error worker.
            ErrorWorker.WorkerReportsProgress = true;
            ErrorWorker.WorkerSupportsCancellation = true;
            ErrorWorker.DoWork += ErrorWorker_DoWork;

            //  Create the process.
            CommandHost = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = info
            };
            CommandHost.Exited += OnProcessExit;

            //  Start the process.
            try
            {
                CommandHost.Start();
            }
            catch (Exception e)
            {
                //  Trace the exception.
                Trace.WriteLine("Failed to start process " + info.FileName + " with arguments '" + info.Arguments + "'");
                Trace.WriteLine(e.ToString());
                return;
            }
            Thread.Sleep(200);

            InputWriter = CommandHost.StandardInput;
            OutputReader = TextReader.Synchronized(CommandHost.StandardOutput);
            ErrorReader = TextReader.Synchronized(CommandHost.StandardError);

            //  Run the workers that read output and error.
            OutputWorker.RunWorkerAsync();
            ErrorWorker.RunWorkerAsync();
        }

        #region Process Events
        private void FireProcessOutputEvent(string content)
        {
            this.WriteOutputToTextBox(content, TextBox.ForeColor);
        }

        private void FireProcessErrorEvent(string content)
        {
            this.WriteOutputToTextBox(content, Color.Red);
        }

        private void FireProcessInputEvent(string content)
        {
            this.WriteInput(content, TextBox.ForeColor, false);
        }

        private void FireProcessExitEvent(int code)
        {
            this.WriteOutputToTextBox($"The process has terminated with the following code: {code}", Color.Blue);
            this.Close();
        }
        #endregion

        #region Input/Output Methods
        private void WriteOutputToTextBox(string output, Color color)
        {
            TextBox.Invoke((Action)(() =>
            {
                TextBox.SelectionColor = color;
                TextBox.AppendText(output);
                Text.Trim();
                TextBox.SelectionStart = Text.Length;
                PositionOfSplitter = TextBox.Text.LastIndexOf('>') + 1;
            }));
        }

        private void WriteInput(string input, Color color, bool echo)
        {
            TextBox.Invoke((Action)(() =>
            {
                if (echo)
                {
                    TextBox.SelectionColor = color;
                    TextBox.AppendText(input);
                    Text.Trim();
                    TextBox.SelectionStart = Text.Length;
                }

                this.WriteToProcess(input);
            }));
        }

        private void WriteToProcess(string input)
        {
            InputWriter.WriteLine(input);
            InputWriter.Flush();   
        }
        #endregion

        private void OnProcessExit(object sender, EventArgs e)
        {
            FireProcessExitEvent(CommandHost.ExitCode);
        }

        /// <summary>
        /// Close the connection and stop the process. Same functionality with Dispose().
        /// </summary>
        public void Close()
        {
            if (!Closed)
            {
                this.Dispose();
                Closed = true;
            }
        }

        private bool HandleColor(string colorcode)
        {
            colorcode = colorcode.ToUpper();
            if (!colorcode.Valid())
            {
                return false;
            }
            if (colorcode.Length != 2)
            {
                return false;
            }
            if (colorcode[0] == colorcode[1])
            {
                return false;
            }
            if (!colorcode.ContainsInvalidCharacters(new char[16] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' }))
            {
                return false;
            }

            switch (colorcode[0])
            {
                case '0':
                    TextBox.BackColor = Color.Black;
                    break;
                case '1':
                    TextBox.BackColor = Color.Blue;
                    break;
                case '2':
                    TextBox.BackColor = Color.Green;
                    break;
                case '3':
                    TextBox.BackColor = Color.Aqua;
                    break;
                case '4':
                    TextBox.BackColor = Color.DarkRed;
                    break;
                case '5':
                    TextBox.BackColor = Color.Purple;
                    break;
                case '6':
                    TextBox.BackColor = Color.Yellow;
                    break;
                case '7':
                    TextBox.BackColor = Color.WhiteSmoke;
                    break;
                case '8':
                    TextBox.BackColor = Color.Gray;
                    break;
                case '9':
                    TextBox.BackColor = Color.LightBlue;
                    break;
                case 'A':
                    TextBox.BackColor = Color.LightGreen;
                    break;
                case 'B':
                    TextBox.BackColor = Color.Turquoise;
                    break;
                case 'C':
                    TextBox.BackColor = Color.Red;
                    break;
                case 'D':
                    TextBox.BackColor = Color.MediumPurple;
                    break;
                case 'E':
                    TextBox.BackColor = Color.LightYellow;
                    break;
                case 'F':
                    TextBox.BackColor = Color.White;
                    break;
            }
            switch (colorcode[1])
            {
                case '0':
                    TextBox.ForeColor = Color.Black;
                    break;
                case '1':
                    TextBox.ForeColor = Color.Blue;
                    break;
                case '2':
                    TextBox.ForeColor = Color.Green;
                    break;
                case '3':
                    TextBox.ForeColor = Color.Aqua;
                    break;
                case '4':
                    TextBox.ForeColor = Color.DarkRed;
                    break;
                case '5':
                    TextBox.ForeColor = Color.Purple;
                    break;
                case '6':
                    TextBox.ForeColor = Color.Yellow;
                    break;
                case '7':
                    TextBox.ForeColor = Color.WhiteSmoke;
                    break;
                case '8':
                    TextBox.ForeColor = Color.Gray;
                    break;
                case '9':
                    TextBox.ForeColor = Color.LightBlue;
                    break;
                case 'A':
                    TextBox.ForeColor = Color.LightGreen;
                    break;
                case 'B':
                    TextBox.ForeColor = Color.Turquoise;
                    break;
                case 'C':
                    TextBox.ForeColor = Color.Red;
                    break;
                case 'D':
                    TextBox.ForeColor = Color.MediumPurple;
                    break;
                case 'E':
                    TextBox.ForeColor = Color.LightYellow;
                    break;
                case 'F':
                    TextBox.ForeColor = Color.White;
                    break;
            }

            return true;
        }

        #region Workers
        private void ErrorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (ErrorWorker.CancellationPending == false)
            {
                int count;
                do
                {
                    char[] buffer = new char[2048];
                    count = ErrorReader.Read(buffer, 0, 2048);
                    StringBuilder builder = new StringBuilder();
                    builder.Append(buffer, 0, count);
                    this.FireProcessErrorEvent(builder.ToString());
                } while (count > 0);
                Thread.Sleep(200);
            }
        }

        private void OutputWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (OutputWorker.CancellationPending == false)
            {
                int count;
                do
                {
                    char[] buffer = new char[4096];
                    count = OutputReader.Read(buffer, 0, 4096);
                    StringBuilder builder = new StringBuilder();
                    builder.Append(buffer, 0, count);
                    this.FireProcessOutputEvent(builder.ToString());
                } while (count > 0);
                Thread.Sleep(200);
            }
        }
        #endregion

        #region TextBox Events
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Closed)
            {
                throw new ObjectDisposedException(nameof(CmdWrapperTextBox), "This instance of WindowsCommandPrompt is closed. Please terminate your application or prohibit further engagement with this object.");
            }

            if (TextBox.SelectionStart >= 1)
            {
                if (TextBox.SelectionStart - 1 == PositionOfSplitter || PositionOfCaret < PositionOfSplitter)
                {
                    // TODO:
                    // Write code to play a sound when the caret is in an invalid area and the user triggers an edit function.
                }
            }

            switch (e.KeyData)
            {
                case Keys.Back: // Backspace key is down
                    if (EnteringText && PositionOfCaret > PositionOfSplitter && PositionOfCaret <= Text.Length) // The caret is in a valid position
                    {
                        // if caret is in a valid position, let the underlying logic delete chars.
                    }
                    else // if caret is NOT in valid region, supress the keypress
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;

                case Keys.Delete: // Delete key is down
                    if (EnteringText && PositionOfCaret >= PositionOfSplitter && PositionOfCaret < Text.Length) // The caret is in valid region
                    {
                        // if caret is in the valid region, let the underlying logic delete chars.
                    }
                    else // if caret is NOT in valid region, supress the keypress
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;

                case Keys.Enter: // Enter key is down
                    Text.Trim();
                    string input = Text.Remove(0, PositionOfSplitter).Replace("\n", string.Empty).Trim();
                    TextBox.Text = Text.Remove(PositionOfSplitter, LengthOfInput);

                    if (input.Contains(" "))
                    {
                        string[] tokens = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (tokens[0] == "title")
                        {
                            ConsoleTitle = tokens[1];
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        else if (tokens[0] == "color")
                        {
                            if (tokens[1].Length == 0)
                            {
                                this.HandleColor("07");
                                e.Handled = true;
                                e.SuppressKeyPress = true;
                            }
                            else if (this.HandleColor(tokens[1]))
                            {
                                e.Handled = true;
                                e.SuppressKeyPress = true;
                            }
                        }
                    }
                    if (input == "cls")
                    {
                        TextBox.Clear();
                        TextBox.AppendText("\nC:\\Windows\\system32>");
                        Text.Trim();
                        TextBox.SelectionStart = Text.Length;
                        PositionOfSplitter = TextBox.Text.LastIndexOf('>') + 1;
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    else
                    {
                        this.FireProcessInputEvent(input);
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    EnteringText = false;
                    break;

                case Keys.Up:
                case Keys.Down:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Left:
                    if (PositionOfCaret == PositionOfSplitter)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;

                default:
                    if (!EnteringText && PositionOfCaret < PositionOfSplitter) // if the caret is not in a valid position, don't do anything and supress keypresses.
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (Closed)
            {
                throw new ObjectDisposedException(nameof(CmdWrapperTextBox), $"This instance of {nameof(CmdWrapperTextBox)} is closed. Please terminate your application or prohibit further engagement with this object.");
            }

            if (e.KeyData == (Keys.H | Keys.Control) || e.KeyData == (Keys.Control | Keys.H))
            {
                this.FireProcessInputEvent("help");
            }
            else if (e.KeyData == (Keys.C | Keys.Control) || e.KeyData == (Keys.Control | Keys.C))
            {
                if (TextBox.SelectedText.Valid())
                {
                    // do not supress keypress and let the underlying logic copy selected text to memory.
                }
                else
                {
                    this.FireProcessExitEvent(0);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyData == (Keys.A | Keys.Control) || e.KeyData == (Keys.Control | Keys.A))
            {
                TextBox.Select(PositionOfSplitter, LengthOfInput);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            EnteringText = true;
            try { LengthOfInput = Text.Length - Text.Substring(0, PositionOfSplitter).Length; } catch { LengthOfInput = 0; }
        }

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (Closed)
            {
                throw new ObjectDisposedException(nameof(CmdWrapperTextBox), "This instance of WindowsCommandPrompt is closed. Please terminate your application or prohibit further engagement with this object.");
            }

            if (!TextBox.SelectedText.Valid() && PositionOfCaret < PositionOfSplitter)
            {
                TextBox.SelectionStart = PositionOfSplitter;
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false;

        /// <summary>
        /// Dispose the class.
        /// </summary>
        /// <param name="disposing">Set to true</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    InputWriter.Close();
                    InputWriter.Dispose();

                    OutputReader.Close();
                    OutputReader.Dispose();

                    ErrorReader.Close();
                    ErrorReader.Dispose();

                    ErrorWorker.CancelAsync();
                    ErrorWorker.Dispose();

                    OutputWorker.CancelAsync();
                    OutputWorker.Dispose();

                    CommandHost.Close();
                    CommandHost.Dispose();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose the class. Closes the connection and terminates the process.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
            Closed = true;
        }
        #endregion
    }
}