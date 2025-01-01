using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        MyCommandLine mCmdLine = new MyCommandLine();
        List<IMyTerminalBlock> mBlocks = new List<IMyTerminalBlock>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var cmds_p = mCmdLine.TryParse(argument);
            if (!cmds_p)
            {
                Echo("ERROR: Parsing command line arguments failed.");
            }
            if (mCmdLine.ArgumentCount < 2)
            {
                Echo("ERROR: The script requires at least 2 arguments.");
            }

            var grid_name = mCmdLine.Argument(0);
            var postfix = mCmdLine.Argument(1);

            bool rename = mCmdLine.Switch("rename");

            if (rename && mCmdLine.ArgumentCount < 3)
            {
                Echo("ERROR: Rename flag requires 3 arguments.");
            }
            var new_postfix = mCmdLine.Argument(2);

            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(mBlocks, x => x.CubeGrid.DisplayName == grid_name);
            if (mBlocks.Count == 0)
            {
                Echo($"ERROR: A grid with name \"{grid_name}\" is not reachable from the current grid.");
            }

            if (rename)
            {
                foreach (var block in mBlocks)
                {
                    int last_no = FindIndexOfLastNumber(block.CustomName);
                    var name_root = block.CustomName.Substring(0, last_no);
                    if (name_root.EndsWith(postfix))
                    {
                        var str = new StringBuilder();
                        str.Append(name_root, 0, last_no - postfix.Length);
                        str.Append(new_postfix);
                        str.Append(block.CustomName, last_no, block.CustomName.Length - last_no);
                        block.CustomName = str.ToString();
                    }
                }
            }
            else
            {
                foreach (var block in mBlocks)
                {
                    int last_no = FindIndexOfLastNumber(block.CustomName);
                    var name_root = block.CustomName.Substring(0, last_no);
                    if (!name_root.EndsWith(postfix))
                    {
                        var str = new StringBuilder(name_root);
                        str.Append(" " + postfix);
                        str.Append(block.CustomName, last_no, block.CustomName.Length - last_no);
                        block.CustomName = str.ToString();
                    }
                }
            }
        }
        
        static int FindIndexOfLastNumber(string s)
        {
            int idx = s.Length;
            int idx_ws = 0;
            bool got_number = false;
            bool got_ws = false;
            for (int i = 0; i < s.Length; ++i)
            {
                switch (s[i])
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        if (got_ws && !got_number)
                        {
                            idx = idx_ws;
                        }
                        got_number = true;
                        got_ws = false;
                        break;

                    case ' ':
                        if (!got_ws)
                        {
                            idx_ws = i;
                        }
                        got_ws = true;
                        got_number = false;
                        break;

                    default:
                        got_ws = false;
                        got_number = false;
                        break;
                }
            }
            return idx;
        }
    }
}
