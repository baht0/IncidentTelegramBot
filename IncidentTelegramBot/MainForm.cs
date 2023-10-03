using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IncidentTelegramBot
{
    public partial class MainForm : Form
    {
        private decimal TimerSec { get; set; } = 0;
        private List<Data> OpenData { get; set; } = new List<Data>();
        private List<Data> CheckedData { get; set; } = new List<Data>();

        public MainForm() => InitializeComponent();

        private async void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(chatTextBox.Text) && !string.IsNullOrEmpty(tokenTextBox.Text))
                {
                    OpenData = await GetTotalData("open");
                    CheckedData = await GetTotalData("checked");

                    lastIdLabel.Text = $"{OpenData.LastOrDefault().ID} - {CheckedData.LastOrDefault().ID}";
                    groupBox1.Enabled = false;
                    TimerSec = timerNumericUpDown.Value;
                    timer.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                ErrorLog(ex, MethodBase.GetCurrentMethod().Name);
            }
        }
        private async void Timer_Tick(object sender, EventArgs e)
        {
            timerLabel.Text = $"Next check: {TimerSec} sec.";
            if (TimerSec == 0)
            {
                TimerSec = timerNumericUpDown.Value;
                await Check();
            }
            TimerSec--;
        }

        private async Task<List<Data>> GetTotalData(string type)
        {
            try
            {
                var settings = new JsonSerializerSettings { DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ" };
                var start = JsonConvert.SerializeObject(DateTime.Today.AddDays(-3).Date, settings).Replace("\"", string.Empty);
                var end = JsonConvert.SerializeObject(DateTime.Today.AddDays(1).Date, settings).Replace("\"", string.Empty);
                string interval = type == "checked" ? $"&timeInterval[]={start}&timeInterval[]={end}" : string.Empty;

                var httpClient = new HttpClient();
                var res = await httpClient.GetStringAsync("http://192.168.114.142:3000/api/get_incidents?isGou=false&fix=true&mob=false&type=" + type + "&zues=%D0%92%D1%81%D0%B5+%D0%97%D0%A3%D0%AD%D0%A1&rues=%D0%92%D1%81%D0%B5+%D0%A0%D0%A3%D0%AD%D0%A1" + interval);

                var json = JObject.Parse(res)["incidents"].ToString();
                var incidents = JObject.Parse(json);
                var data = JArray.Parse(incidents["data"].ToString());
                var list = data.ToObject<List<Data>>().OrderBy(x => x.ID).ToList();

                if (type == "open")
                    return list.Where(x => x.DataStart >= DateTime.Today.AddDays(-3).Date).ToList();
                return list;
            }
            catch (Exception ex)
            {
                ErrorLog(ex, MethodBase.GetCurrentMethod().Name);
                return new List<Data>();
            }
        }

        //Check
        private async Task Check()
        {
            try
            {
                //Open
                var _open = await GetTotalData("open");
                foreach (var item in _open)
                {
                    var old = OpenData.FirstOrDefault(x => x.ID == item.ID);
                    if (!OpenData.Any(x => x.ID == item.ID))
                    {
                        var id = await NewIncident(item);
                        item.MessageId = id;
                        OpenData.Add(item);
                    }
                    else if (old != null && old.Problem != item.Problem && old.CountSym < item.CountSym)
                    {
                        var index = OpenData.FindIndex(c => c == old);
                        var id = await EditIncident(item, old?.MessageId);
                        item.MessageId = id;
                        OpenData[index] = item;
                    }
                }

                //Checked
                var _checked = await GetTotalData("checked");
                foreach (var item in _checked)
                {
                    if (!CheckedData.Any(x => x.ID == item.ID))
                    {
                        var open = OpenData.FirstOrDefault(x => x.ID == item.ID);
                        await CheckedIncident(item, open?.MessageId);
                        CheckedData.Add(item);
                        if (open != null)
                            OpenData.Remove(item);
                    }
                }

                //Save
                var openList = OpenData.OrderBy(x => x.ID).ToList();
                OpenData = openList.Where(x => x.DataStart >= DateTime.Today.AddDays(-3).Date).ToList();
                var checkedList = CheckedData.OrderBy(x => x.ID).ToList();
                CheckedData = checkedList.Where(x => x.DataEnd >= DateTime.Today.AddDays(-3).Date).ToList();

                lastIdLabel.Text = $"{OpenData.LastOrDefault().ID} - {CheckedData.LastOrDefault().ID}";
            }
            catch (Exception ex)
            {
                ErrorLog(ex, MethodBase.GetCurrentMethod().Name);
            }
        }
        private async Task<int> NewIncident(Data data)
        {
            var message = new StringBuilder();
            message.AppendLine("\U00002757 <b>Инцидент создан:</b>\n");
            message.AppendLine($"<b>ID:</b> {data.ID}");
            message.AppendLine($"<b>ЗУЭС:</b> {data.Zues}");
            message.AppendLine($"<b>РУЭС:</b> {data.Rues}");
            message.AppendLine($"<b>Адрес:</b> {data.Address}");
            message.AppendLine($"<b>Дата начала:</b> {data.DataStart}");
            message.AppendLine($"<b>Проблема:</b> {data.Problem}");
            message.AppendLine($"<b>Всего услуг в простое:</b> {data.UslugiAll}");
            message.AppendLine($"<b>IP:</b> {data.IP}");
            return await SendMessage(message.ToString());
        }
        private async Task<int> EditIncident(Data data, int? messageId)
        {
            var message = new StringBuilder();
            message.AppendLine("\U0000270F <b>Инцидент изменен:</b>\n");
            if (messageId != null)
            {
                message.AppendLine($"<b>ID:</b> {data.ID}");
                message.AppendLine($"<b>Проблема:</b> {data.Problem}");
                return await ReplyMessage(message.ToString(), messageId);
            }
            message.AppendLine($"<b>ID:</b> {data.ID}");
            message.AppendLine($"<b>ЗУЭС:</b> {data.Zues}");
            message.AppendLine($"<b>РУЭС:</b> {data.Rues}");
            message.AppendLine($"<b>Адрес:</b> {data.Address}");
            message.AppendLine($"<b>Дата начала:</b> {data.DataStart}");
            message.AppendLine($"<b>Проблема:</b> {data.Problem}");
            message.AppendLine($"<b>Всего услуг в простое:</b> {data.UslugiAll}");
            message.AppendLine($"<b>IP:</b> {data.IP}");
            return await SendMessage(message.ToString());
        }
        private async Task CheckedIncident(Data data, int? messageId)
        {
            var message = new StringBuilder();
            message.AppendLine("\U00002705 <b>Инцидент обработан:</b>\n");
            if (messageId != null)
            {
                message.AppendLine($"<b>ID:</b> {data.ID}");
                message.AppendLine($"<b>Дата конца:</b> {data.DataEnd}");
                message.AppendLine($"<b>Проблема:</b> {data.Problem}");
                await ReplyMessage(message.ToString(), messageId);
            }
            message.AppendLine($"<b>ID:</b> {data.ID}");
            message.AppendLine($"<b>ЗУЭС:</b> {data.Zues}");
            message.AppendLine($"<b>РУЭС:</b> {data.Rues}");
            message.AppendLine($"<b>Адрес:</b> {data.Address}");
            message.AppendLine($"<b>Дата начала:</b> {data.DataStart}");
            message.AppendLine($"<b>Дата конца:</b> {data.DataEnd}");
            message.AppendLine($"<b>Проблема:</b> {data.Problem}");
            message.AppendLine($"<b>Всего услуг в простое:</b> {data.UslugiAll}");
            message.AppendLine($"<b>IP:</b> {data.IP}");
            await SendMessage(message.ToString());
        }

        //Telegram API
        private async Task<int> SendMessage(string message)
        {
            string chat_id = chatTextBox.Text;
            string bot_token = tokenTextBox.Text;

            var content = new MultipartFormDataContent()
            {
                    { new StringContent(chat_id, Encoding.UTF8), "chat_id" },
                    { new StringContent("HTML", Encoding.UTF8), "parse_mode" },
                    { new StringContent(message, Encoding.UTF8), "text" }
            };

            var client = new HttpClient();
            var res = await client.PostAsync("https://api.telegram.org/bot" + bot_token + "/sendMessage?", content);

            var json = await res.Content.ReadAsStringAsync();
            return Convert.ToInt32(JObject.Parse(json)["result"]["message_id"]);
        }
        private async Task<int> ReplyMessage(string message, int? message_id)
        {
            string chat_id = chatTextBox.Text;
            string bot_token = tokenTextBox.Text;

            var content = new MultipartFormDataContent()
            {
                    { new StringContent(chat_id, Encoding.UTF8), "chat_id" },
                    { new StringContent("HTML", Encoding.UTF8), "parse_mode" },
                    { new StringContent(message_id.ToString(), Encoding.UTF8), "reply_to_message_id" },
                    { new StringContent(message, Encoding.UTF8), "text" }
            };

            var client = new HttpClient();
            var res = await client.PostAsync("https://api.telegram.org/bot" + bot_token + "/sendMessage?", content);

            var json = await res.Content.ReadAsStringAsync();
            return Convert.ToInt32(JObject.Parse(json)["result"]["message_id"]);
        }

        //Exception logs
        private void ErrorLog(Exception exp, string method)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "incidentTelegramBot_exp.txt";
            string str = exp.Message + "\n" + exp.StackTrace;
            if (!File.Exists(path))
                File.WriteAllText(path, $"[{DateTime.Now}] | {method} |\n{str}\n");
            else
                File.WriteAllText(path, string.Format("{0}{1}", $"[{DateTime.Now}]\n{str}\n", File.ReadAllText(path)));
        }
    }
}