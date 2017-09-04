﻿using GW2MH.Core.Data;
using GW2MH.Core.Memory;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GW2MH.Views
{
    public partial class FrmMain : Form
    {

        public bool IsSpeedhackEnabled { get; private set; }
        public bool IsFlyhackEnabled { get; private set; }

        public Process TargetProcess { get; private set; }
        public MemSharp Memory { get; private set; }

        internal CharacterData CharacterData { get; private set; }

        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            ttDefault.SetToolTip(numBaseSpeedMultiplier, "If Speedhack is enabled, this defines the speed in percent how fast your character is moving. (Default 100%).");
            ttDefault.SetToolTip(numExtSpeedMultiplier, "If Speedhack is enabled and Left Shift is pressed, then it multiplies your speed using this value.");
        }

        private async void FrmMain_Shown(object sender, EventArgs e)
        {
            var processes = Process.GetProcessesByName("Gw2-64");
            if(processes.Length == 0)
            {
                MessageBox.Show("Guild Wars 2 (64 Bit) seems not to be running, please launch Guild Wars 2 first.", "Game client missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
            else
            {
                TargetProcess = processes[0];
                Memory = new MemSharp(TargetProcess);

                MemoryData.ContextPtr = await Task.Factory.StartNew(() =>
                {
                    var contextPtr = IntPtr.Zero;
                    var contextCalcPtr = Memory.Pattern(Memory.TargetProcess.MainModule, MemoryData.ContextCalcPattern);

                    if(contextCalcPtr != IntPtr.Zero)
                    {
                        var jumpSize = (uint)MemoryData.ContextCalcJumpPatch(IntPtr.Zero).Length;

                        IntPtr jumpLocation = Native.VirtualAllocEx(Memory.TargetProcess.Handle, IntPtr.Zero, jumpSize, Native.AllocationTypeFlags.MEM_COMMIT, Native.MemoryProtectionFlags.PAGE_EXECUTE_READ_WRITE);
                        IntPtr pointerLocation = Native.VirtualAllocEx(Memory.TargetProcess.Handle, IntPtr.Zero, (uint)IntPtr.Size, Native.AllocationTypeFlags.MEM_COMMIT, Native.MemoryProtectionFlags.PAGE_READ_WRITE);
                        if (jumpLocation != IntPtr.Zero && pointerLocation != IntPtr.Zero)
                        {
                            Memory.Write(jumpLocation, MemoryData.ContextCalcJumpShellCode(pointerLocation, contextCalcPtr + MemoryData.ContextCalcJumpPatchOffset + 13));
                            Memory.Write(contextCalcPtr + MemoryData.ContextCalcJumpPatchOffset, MemoryData.ContextCalcJumpPatch(jumpLocation));

                            while (contextPtr == IntPtr.Zero) { contextPtr = new IntPtr(Memory.Read<long>(pointerLocation)); }

                            Memory.Write(contextCalcPtr + MemoryData.ContextCalcJumpPatchOffset, MemoryData.ContextCalcRestore);
                        }
                    }

                    return contextPtr;
                });

                if(MemoryData.ContextPtr != IntPtr.Zero)
                {
                    lbStatus.Text = string.Format("Status: Found Context @ 0x{0:X8}", MemoryData.ContextPtr.ToInt64());
                    InitialTick();
                    tmrUpdater.Start();
                }
                else
                {
                    MessageBox.Show("Unable to find Context Pointer, please contact the administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }
            }
        }

        private void InitialTick()
        {
            if(Memory.IsRunning)
            {
                CharacterData = new CharacterData();
                CharacterData.DefaultMoveSpeed = Memory.Read<float>(MemoryData.ContextPtr, MemoryData.MoveSpeedOffsets);
            }
        }

        private void FinalTick()
        {
            if(Memory.IsRunning)
            {
                // Reset Move Speed
                Memory.Write(MemoryData.ContextPtr, MemoryData.MoveSpeedOffsets, CharacterData.DefaultMoveSpeed);
            }
        }

        private void tmrUpdater_Tick(object sender, EventArgs e)
        {
            if (Memory.IsRunning)
            {
                if(cbSpeedhack.Checked)
                {
                    if (Convert.ToBoolean(Native.GetAsyncKeyState(Keys.LShiftKey) & 0x8000))
                        Memory.Write(MemoryData.ContextPtr, MemoryData.MoveSpeedOffsets, CharacterData.DefaultMoveSpeed * ((float)numExtSpeedMultiplier.Value / 100f));
                    else
                        Memory.Write(MemoryData.ContextPtr, MemoryData.MoveSpeedOffsets, CharacterData.DefaultMoveSpeed * ((float)numBaseSpeedMultiplier.Value / 100f));
                }
                else
                    Memory.Write(MemoryData.ContextPtr, MemoryData.MoveSpeedOffsets, CharacterData.DefaultMoveSpeed);
            }
            else
            {
                tmrUpdater.Stop();
                MessageBox.Show("Guild Wars 2 has closed, GW2MH-R will close now.", "Bye", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
        }

        private void btnDonate_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=EHZVSBXL7X2Q6");
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new FrmSettings().ShowDialog();
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            FinalTick();

            if(tmrUpdater.Enabled)
                tmrUpdater.Stop();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}