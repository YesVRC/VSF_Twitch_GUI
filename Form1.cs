using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpOSC;


namespace VSF_Twitch_GUI
{
    

    public partial class Form1 : Form
    {
        

        public Form1()
        {
            InitializeComponent();
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var message = new OscMessage("TOsc/Test", 0, 0, 1);
            var OSCSender= new UDPSender("127.0.0.1", 3334);
            OSCSender.Send(message);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var message = new OscMessage("TOsc/rainbow", 0, 0, 1, 0);
            var OSCSender = new UDPSender("127.0.0.1", 3334);
            OSCSender.Send(message);
        }

        public void FormLog(string message)
        {
            richTextBox1.Text += $"{message} \n";
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            var message = new OscMessage("TOsc/rainbow", 0, 0, 1, 1);
            var OSCSender = new UDPSender("127.0.0.1", 3334);
            OSCSender.Send(message);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var message = new OscMessage("TOsc/rainbow", 0, 0, 1, 2);
            var OSCSender = new UDPSender("127.0.0.1", 3334);
            OSCSender.Send(message);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
