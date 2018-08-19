using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace GpsPreview
{
	public class ControlWriter : TextWriter
	{
		private TextBlock textbox;
		public ControlWriter(TextBlock textbox)
		{
			this.textbox = textbox;
		}

		public override void Write(char value)
		{
			textbox.Text += value;
		}

		public override void Write(string value)
		{
			textbox.Text += value;
		}

		public override Encoding Encoding
		{
			get { return Encoding.ASCII; }
		}
	}
}
