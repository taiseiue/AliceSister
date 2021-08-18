﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AliceScript;
using System.IO;

namespace alice
{
    public class SecondProgram
    {
        const string EXT = "WSOFTScript";

        enum NEXT_CMD
        {
            NONE = 0,
            PREV = -1,
            NEXT = 1,
            TAB = 2
        };
        public static bool canDebug = false;
        [STAThread]
        public static void Do(string[] args)

        {


            Alice.Exiting += Alice_Exiting;
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine();
                Console.WriteLine("何かキーを押して終了します。");
                Console.ReadKey();
                Environment.Exit(0);
            };

            ClearLine();

            // Subscribe to the printing events from the interpreter.
            // A printing event will be triggered after each successful statement
            // execution. On error an exception will be thrown.
            Interpreter.Instance.OnOutput += Print;

            //例外スローを静かにする
            ThrowErrorManerger.HandleError = true;
            ThrowErrorManerger.ThrowError += ThrowErrorManerger_ThrowError;

            string scriptFilename = "scripts/temp.alice";
            scriptFilename = "";
            string script = Utils.GetFileContents(scriptFilename);

            Variable resultprop = Interpreter.Instance.Process("print(\"AliceScript バージョン\"+wsver);\r\nprint(\"(c) 2021 WSOFT All rights reserved.\");");

            
            Console.WriteLine();




            if (args.Length >= 3)
            {
                Translation.TranslateScript(args);
                return;
            }
            if (args.Length > 0)
            {
                if (args[0].EndsWith(EXT))
                {
                    scriptFilename = args[0];
                    Console.WriteLine("Reading script from " + scriptFilename);
                    script = Utils.GetFileContents(scriptFilename);
                }
                else
                {
                    script = args[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(script))
            {
                ProcessScript(script, scriptFilename);
                return;
            }

            RunLoop();
        }

        private static void Alice_Exiting(object sender, ExitingEventArgs e)
        {

        }

     
        private static void ThrowErrorManerger_ThrowError(object sender, ThrowErrorEventArgs e)
        {

            Utils.PrintColor("実行中のエラー:" + e.Message + " 行" + e.Script.OriginalLineNumber + " コード:" + e.Script.OriginalLine + " ファイル名:" + e.Script.Filename + "\r\n", ConsoleColor.Red);
            Dictionary<string, Variable> dic = Debug.Variables;
            Console.WriteLine("変数の内容\r\n| 変数名 | 内容 |");
            foreach (string s in dic.Keys)
            {
                Console.WriteLine("| " + s + " | " + dic[s].AsString() + " |");
            }
        }

        private static void SplitByLast(string str, string sep, ref string a, ref string b)
        {
            int it = str.LastIndexOfAny(sep.ToCharArray());
            a = it == -1 ? "" : str.Substring(0, it + 1);
            b = it == -1 ? str : str.Substring(it + 1);
        }

        private static string CompleteTab(string script, string init, ref int tabFileIndex,
          ref string start, ref string baseStr, ref string startsWith)
        {
            if (tabFileIndex > 0 && !script.Equals(init))
            {
                // The user has changed something in the input field
                tabFileIndex = 0;
            }
            if (tabFileIndex == 0 || script.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                // The user pressed tab the first time or pressed it on a directory
                string path = "";
                SplitByLast(script, " ", ref start, ref path);
                SplitByLast(path, "/\\", ref baseStr, ref startsWith);
            }

            tabFileIndex++;
            string result = Utils.GetFileEntry(baseStr, tabFileIndex, startsWith);
            result = result.Length == 0 ? startsWith : result;
            return start + baseStr + result;
        }
        static bool exit = false;
        private static void RunLoop()
        {
            List<string> commands = new List<string>();
            StringBuilder sb = new StringBuilder();
            int cmdPtr = 0;
            int tabFileIndex = 0;
            bool arrowMode = false;
            string start = "", baseCmd = "", startsWith = "", init = "", script;
            string previous = "";

            while (!exit)
            {
                sb.Clear();

                NEXT_CMD nextCmd = NEXT_CMD.NONE;
                script = previous + GetConsoleLine(ref nextCmd, init).Trim();

                if (script.EndsWith(Constants.CONTINUE_LINE.ToString()))
                {
                    previous = script.Remove(script.Length - 1);
                    init = "";
                    continue;
                }

                if (nextCmd == NEXT_CMD.PREV || nextCmd == NEXT_CMD.NEXT)
                {
                    if (arrowMode || nextCmd == NEXT_CMD.NEXT)
                    {
                        cmdPtr += (int)nextCmd;
                    }
                    cmdPtr = cmdPtr < 0 || commands.Count == 0 ?
                             cmdPtr + commands.Count :
                             cmdPtr % commands.Count;
                    init = commands.Count == 0 ? script : commands[cmdPtr];
                    arrowMode = true;
                    continue;
                }
                else if (nextCmd == NEXT_CMD.TAB)
                {
                    init = CompleteTab(script, init, ref tabFileIndex,
                             ref start, ref baseCmd, ref startsWith);
                    continue;
                }

                init = "";
                previous = "";
                tabFileIndex = 0;
                arrowMode = false;

                if (string.IsNullOrWhiteSpace(script))
                {
                    continue;
                }

                if (commands.Count == 0 || !commands[commands.Count - 1].Equals(script))
                {
                    commands.Add(script);
                }
                if (!script.EndsWith(Constants.END_STATEMENT.ToString()))
                {
                    script += Constants.END_STATEMENT;
                }

                ProcessScript(script);
                cmdPtr = commands.Count - 1;
            }
        }

        static string GetConsoleLine(ref NEXT_CMD cmd, string init = "",
                                             bool enhancedMode = true)
        {
            //string line = init;
            StringBuilder sb = new StringBuilder(init);
            int delta = init.Length - 1;
            string prompt = GetPrompt();
            Console.Write(prompt);
            Console.Write(init);

            if (!enhancedMode)
            {
                return Console.ReadLine();
            }

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.UpArrow)
                {
                    cmd = NEXT_CMD.PREV;
                    ClearLine(prompt, sb.ToString());
                    return sb.ToString();
                }
                if (key.Key == ConsoleKey.DownArrow)
                {
                    cmd = NEXT_CMD.NEXT;
                    ClearLine(prompt, sb.ToString());
                    return sb.ToString();
                }
                if (key.Key == ConsoleKey.RightArrow)
                {
                    delta = Math.Max(-1, Math.Min(++delta, sb.Length - 1));
                    SetCursor(prompt, sb.ToString(), delta + 1);
                    continue;
                }
                if (key.Key == ConsoleKey.LeftArrow)
                {
                    delta = Math.Max(-1, Math.Min(--delta, sb.Length - 1));
                    SetCursor(prompt, sb.ToString(), delta + 1);
                    continue;
                }
                if (key.Key == ConsoleKey.Tab)
                {
                    cmd = NEXT_CMD.TAB;
                    ClearLine(prompt, sb.ToString());
                    return sb.ToString();
                }
                if (key.Key == ConsoleKey.Backspace || key.Key == ConsoleKey.Delete)
                {
                    if (sb.Length > 0)
                    {
                        delta = key.Key == ConsoleKey.Backspace ?
                          Math.Max(-1, Math.Min(--delta, sb.Length - 2)) : delta;
                        if (delta < sb.Length - 1)
                        {
                            sb.Remove(delta + 1, 1);
                        }
                        SetCursor(prompt, sb.ToString(), Math.Max(0, delta + 1));
                    }
                    continue;
                }
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return sb.ToString();
                }
                if (key.KeyChar == Constants.EMPTY)
                {
                    continue;
                }

                ++delta;
                Console.Write(key.KeyChar);
                if (delta < sb.Length)
                {
                    delta = Math.Max(0, Math.Min(delta, sb.Length - 1));
                    sb.Insert(delta, key.KeyChar.ToString());
                }
                else
                {
                    sb.Append(key.KeyChar);
                }
                SetCursor(prompt, sb.ToString(), delta + 1);
            }
        }

        private static void ProcessScript(string script, string filename = "")
        {
            s_PrintingCompleted = false;
            string errorMsg = null;
            Variable result = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    result = System.Threading.Tasks.Task.Run(() =>
                  Interpreter.Instance.ProcessFileAsync(filename, true)).Result;
                    //Interpreter.Instance.ProcessFile(filename, true)).Result;
                }
                else
                {
                    result = System.Threading.Tasks.Task.Run(() =>
                      //Interpreter.Instance.ProcessAsync(script, filename)).Result;
                      Interpreter.Instance.Process(script, filename, true)).Result;
                }
            }
            catch (Exception exc)
            {
                errorMsg = exc.InnerException != null ? exc.InnerException.Message : exc.Message;
                ParserFunction.InvalidateStacksAfterLevel(0);
            }

            if (!s_PrintingCompleted)
            {
                string output = Interpreter.Instance.Output;
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine(output);
                }
                else if (result != null)
                {
                    output = result.AsString(false, false);
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Console.WriteLine(output);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(errorMsg))
            {
                Utils.PrintColor(errorMsg + Environment.NewLine, ConsoleColor.Red);
                errorMsg = string.Empty;
            }
        }

        private static string GetPrompt()
        {
            const int MAX_SIZE = 30;
            string path = Directory.GetCurrentDirectory();
            /*
            if (path.Length > MAX_SIZE)
            {
                path = "..." + path.Substring(path.Length - MAX_SIZE);
            }
            */

            return string.Format("{0}>>", path);
        }

        private static void ClearLine(string part1 = "", string part2 = "")
        {
            string spaces = new string(' ', part1.Length + part2.Length + 1);
            Console.Write("\r{0}\r", spaces);
        }

        private static void SetCursor(string prompt, string line, int pos)
        {
            ClearLine(prompt, line);
            Console.Write("{0}{1}\r{2}{3}",
              prompt, line, prompt, line.Substring(0, pos));
        }

        static void Print(object sender, OutputAvailableEventArgs e)
        {
            Console.Write(e.Output);
            s_PrintingCompleted = true;
        }
        static bool s_PrintingCompleted = false;
    }
}
