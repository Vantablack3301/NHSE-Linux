using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Gtk;

namespace NHSE.WinForms
{
    internal static class WinFormsUtil
    {
        internal static void TranslateInterface(Window form, string lang) => form.TranslateInterface(lang);

        #region Message Displays
        /// <summary>
        /// Displays a dialog showing the details of an error.
        /// </summary>
        /// <param name="lines">User-friendly message about the error.</param>
        /// <returns>The <see cref="ResponseType"/> associated with the dialog.</returns>
        internal static ResponseType Error(params string[] lines)
        {
            System.Media.SystemSounds.Hand.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
            return MessageDialog.New(null, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, msg).Run();
        }

        internal static ResponseType Alert(params string[] lines) => Alert(true, lines);

        internal static ResponseType Alert(bool sound, params string[] lines)
        {
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
            return MessageDialog.New(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, msg).Run();
        }

        internal static ResponseType Prompt(ButtonsType btn, params string[] lines)
        {
            System.Media.SystemSounds.Asterisk.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines);
            return MessageDialog.New(null, DialogFlags.Modal, MessageType.Question, btn, msg).Run();
        }
        #endregion

        public static T? GetUnderlyingControl<T>(object sender) where T : Widget
        {
            while (true)
            {
                switch (sender)
                {
                    case MenuItem t:
                        sender = t.Parent;
                        continue;
                    case Menu c:
                        sender = c.AttachWidget;
                        continue;
                    case T p:
                        return p;
                    default:
                        return default;
                }
            }
        }

        /// <summary>
        /// Gets the selected value of the input <see cref="cb"/>. If no value is selected, will return 0.
        /// </summary>
        /// <param name="cb">ComboBox to retrieve value for.</param>
        internal static int GetIndex(ComboBox cb) => cb.Active;

        public static T? FirstFormOfType<T>() where T : Window => (T?)Application.OpenForms.Cast<Window>().FirstOrDefault(form => form is T);

        public static void RemoveDropCB(object sender, KeyEventArgs e) => ((ComboBox)sender).PopupShown = false;

        /// <summary>
        /// Centers the <see cref="child"/> horizontally and vertically so that its center is the same as the <see cref="parent"/>'s center.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        internal static void CenterToForm(this Widget child, Widget parent)
        {
            int x = parent.Allocation.X + ((parent.Allocation.Width - child.Allocation.Width) / 2);
            int y = parent.Allocation.Y + ((parent.Allocation.Height - child.Allocation.Height) / 2);
            child.SetSizeRequest(Math.Max(x, 0), Math.Max(y, 0));
        }
    }
}
