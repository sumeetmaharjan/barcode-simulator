using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace BarcodeSimulator
{

    public partial class BarcodeSimulatorControl : Form
    {
        private readonly string FileName = "codes.txt";

        public BarcodeSimulatorControl()
        {
            InitializeComponent();
        }

        private void BarcodeSimulatorControl_Load(object sender, EventArgs e)
        {
            // setup a new Hotkey to watch for Windows+Z
            // this could/should be configurable on the form
            var hk = new Hotkey(Keys.U, shift: false, control: true, alt: true, windows: false);
            hk.Pressed += HotkeyPressed;
            hk.Register(this);

            // show the hotkey on the form as an FYI
            hotkeyTextBox.Text = hk.ToString();

            // list all available keys in the 'ends with' drop down
            Enum.GetNames(typeof(Keys)).ToList().ForEach(k => endsWithComboBox.Items.Add(k));

            endsWithComboBox.SelectedItem = endsWithComboBox.Items[11];

            // focus on the text box for adding new string
            ActiveControl = newStringTextBox;

            SetupToolTips();
            btnTriggerAfterDelay.Text = "Trigger after " + triggerDelay.Value + " seconds";
            LoadCodesFromFile();

            // itemsListView.SelectedIndexChanged += ItemsListView_SelectedIndexChanged;

        }

        private void ItemsListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var temp = (ListView)sender;
            if (temp.Focused)
            {
                var focusedItem = temp.SelectedItems[0].Index;


            }
            //var tee = temp.SelectedIndices[0];
            Console.WriteLine();
        }

        private void LoadCodesFromFile()
        {
            try
            {
                var codes = File.ReadAllLines(FileName);
                foreach (var code in codes)
                {
                    if (string.IsNullOrEmpty(code)) continue;
                    itemsListView.Items.Add(new ListViewItem(new[] { code, Barcode.GetTypeName(code) }));
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void SetupToolTips()
        {
            var tt = new ToolTip { InitialDelay = 500, ReshowDelay = 500, ShowAlways = true };

            tt.SetToolTip(delayNumeric, "Delay in milliseconds between each keypress when sending a barcode.");
            tt.SetToolTip(hotkeyTextBox, "Activation key sequence. Press " + hotkeyTextBox.Text + " to send the next barcode.");
            tt.SetToolTip(endsWithComboBox, "Optionally ends each barcode sending with this key.");
            tt.SetToolTip(newStringTextBox, "Enter a series of characters you want to simulate. Press Enter to add it.");
            tt.SetToolTip(itemsListView, "List of barcodes to send. Sends in order round-robin style. Select one and press Delete to remove it.");
        }

        private void HotkeyPressed(object sender, HandledEventArgs e)
        {
            if (itemsListView.Items.Count == 0)
            {
                MessageBox.Show("You have to add some strings to the list first.", "Fail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var endKey = Keys.None;

            if (endsWithComboBox.SelectedIndex > 0)
                Enum.TryParse(endsWithComboBox.SelectedItem.ToString(), out endKey);

            Thread.Sleep(2000);

            var s = GetNextCode();

            // do the delayed key sending in a separate thread so we don't hang the window
            ThreadStart starter = () => StartSending(s, (int)delayNumeric.Value, endKey);
            var t = new Thread(starter) { Name = "Sending keys " + s };
            t.Start();
        }

        private static void StartSending(string text, int delay, Keys endKey = Keys.None)
        {

            foreach (var s in text.Select(character => character.ToString()))
            {
                Debug.WriteLine("{0} Sending text '{1}'", DateTime.Now.ToString("HH:mm:ss.fff"), s);
                SendKeys.SendWait(s);
                SendKeys.Flush();
                Thread.Sleep(delay);
            }

            // if configured, send an 'end' key to signal that we're at the end of the barcode
            if (endKey != Keys.None)
                SendKeys.SendWait("{" + Enum.GetName(typeof(Keys), endKey) + "}");

            // beep!
            System.Media.SystemSounds.Beep.Play();
        }

        /// <summary>
        /// Get the next value from the list of items and advance the selection
        /// If we're at the end of the list, go back to the beginning
        /// </summary>
        /// <returns></returns>
        private string GetNextCode()
        {
            if (itemsListView.SelectedItems.Count == 0)
            {
                itemsListView.Items[0].Selected = true;
                itemsListView.Select();
            }

            var currentIndex = itemsListView.SelectedItems[0].Index;

            var s = itemsListView.Items[currentIndex].Text;

            if (currentIndex == itemsListView.Items.Count - 1)
            {
                itemsListView.Items[currentIndex].Selected = false;
                itemsListView.Items[0].Selected = true;
            }
            else
                itemsListView.Items[currentIndex + 1].Selected = true;

            itemsListView.Select();

            return s;
        }

        /// <summary>
        /// Ignore all key entry aside from Enter. Enter adds the value to the
        /// string list and clears the new string entry text box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newStringTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            var code = newStringTextBox.Text;

            itemsListView.Items.Add(new ListViewItem(new[] { code, Barcode.GetTypeName(code) }));


            try
            {
                using (FileStream fs = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    StreamWriter tw = new StreamWriter(fs);
                    foreach (ListViewItem item in itemsListView.Items)
                    {
                        tw.WriteLine(item.Text);
                    }
                    tw.Flush();
                }
            }
            catch { }

            newStringTextBox.Clear();
        }

        /// <summary>
        /// Handle deleting items from the string list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void itemsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete)
                return;

            if (itemsListView.SelectedItems.Count == 0)
                return;

            itemsListView.Items.Remove(itemsListView.SelectedItems[0]);
        }

        private void newStringTextBox_TextChanged(object sender, EventArgs e)
        {
            var text = newStringTextBox.Text;

            if (string.IsNullOrEmpty(text))
            {
                newCodeTypeLabel.Text = null;
                return;
            }

            var type = Barcode.GetTypeName(text);

            if (newCodeTypeLabel.Text == type)
                return;

            //newCodeTypeLabel.Text = type;

        }

        private void btnTriggerAfterDelay_Click(object sender, EventArgs e)
        {
            Thread.Sleep((int)(triggerDelay.Value * 1000));
            if (itemsListView.Items.Count == 0)
            {
                MessageBox.Show("You have to add some strings to the list first.", "Fail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var endKey = Keys.None;

            if (endsWithComboBox.SelectedIndex > 0)
                Enum.TryParse(endsWithComboBox.SelectedItem.ToString(), out endKey);

            var s = GetNextCode();

            // do the delayed key sending in a separate thread so we don't hang the window
            ThreadStart starter = () => StartSending(s, (int)delayNumeric.Value, endKey);
            var t = new Thread(starter) { Name = "Sending keys " + s };
            t.Start();
        }

        private void triggerDelay_ValueChanged(object sender, EventArgs e)
        {
            btnTriggerAfterDelay.Text = "Trigger after " + triggerDelay.Value + " seconds";
        }
    }
}
