using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remote.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Remote
{
    internal static class Config
    {
        internal static string AppName = Properties.Settings.Default.AppName;
        internal static string IpAddr = Properties.Settings.Default.IpAddr;
        internal static string MacAddr = Properties.Settings.Default.MacAddr;
        internal static int Port = Properties.Settings.Default.Port;
        internal static string Token = Properties.Settings.Default.Token;
    }

    public class Command
    {
        public string Method { get; set; }
        public Params Params { get; set; }
    }

    public class Params
    {
        public string Cmd { get; set; }
        public string DataOfCmd { get; set; }
        public string Option { get; set; }
        public string TypeOfRemote { get; set; }
        public string Data { get; set; }
        public string Event { get; set; }
        public string To { get; set; }
    }

    internal class Samsung
    {
        private string wsUrl;
        private Dictionary<string, string> keys;

        public Samsung()
        {
            //Properties.Settings.Default.Reset();
            InitializeKeys();
            wsUrl = GetWsUrl();
            Debug.WriteLine(wsUrl);

            Task<bool> task = Task.Run(() => IsActive());
            task.Wait();
            bool active = task.Result;

            Debug.WriteLine("Active: " + active);
            if (!active)
            { 
                WolUtilities.SendTenMagicPackets();
                Task.Delay(2000).Wait();
            }

            WebSocket ws = new WebSocket(wsUrl);
            
            /*ws.OnOpen += (sender, e) =>
            {
            };*/

            ws.OnMessage += (sender, e) =>
            {
                Debug.WriteLine("Msg: " + e.Data);
                JObject response = JObject.Parse(e.Data);
                string temp = response?["data"]?["token"]?.ToString() ?? "token";
                
                Properties.Settings.Default.Token = temp;
            };

            ws.OnError += (sender, e) =>
            {
                Debug.WriteLine("Error: " + e.Message);
            };

            ws.OnClose += (sender, e) =>
            {
                Debug.WriteLine("Close: " + e.Reason);
            };

            ws.Connect();

            Command c = GetCommandByKey("KEY_HOME");
            string cmd = JsonConvert.SerializeObject(c, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }).Replace("Method","method").Replace("Params","params");
            Debug.WriteLine("Send: " + cmd);

            ws.Send(cmd);
        }

        private void wsSendCompleted (bool res)
        {
            Debug.WriteLine($"webSocket send complete {res}");
        }

        private Command GetCommandByKey(string key)
        {
            Command cmd = new Command();
            cmd.Method = "ms.remote.control";
            cmd.Params = new Params();
            cmd.Params.Cmd = "Click";
            cmd.Params.DataOfCmd = key;
            cmd.Params.Option = "false";
            cmd.Params.TypeOfRemote = "SendRemoteKey";
            return cmd;
        }

        private void Send(string key, string eventHandle)
        {

            /*
             const ws = new WebSocket(this.WS_URL, { rejectUnauthorized: false });
        this.LOGGER.log('command', command, '_send');
        this.LOGGER.log('wsUrl', this.WS_URL, '_send');
        ws.on('open', () => {
            if (this.PORT === 8001) {
                setTimeout(() => ws.send(JSON.stringify(command)), 1000);
            }
            else {
                ws.send(JSON.stringify(command));
            }
        });
        ws.on('message', (message) => {
            const data = JSON.parse(message);
            this.LOGGER.log('data: ', JSON.stringify(data, null, 2), 'ws.on message');
            if (done && (data.event === command.params.event || data.event === eventHandle)) {
                this.LOGGER.log('if correct event', JSON.stringify(data, null, 2), 'ws.on message');
                done(null, data);
            }
            if (data.event !== 'ms.channel.connect') {
                this.LOGGER.log('if not correct event', JSON.stringify(data, null, 2), 'ws.on message');
                ws.close();
            }
        });
        ws.on('response', (response) => {
            this.LOGGER.log('response', response, 'ws.on response');
        });
        ws.on('error', (err) => {
            let errorMsg = '';
            if (err.code === 'EHOSTUNREACH' || err.code === 'ECONNREFUSED') {
                errorMsg = 'TV is off or unavailable';
            }
            console.error(errorMsg);
            this.LOGGER.error(errorMsg, err, 'ws.on error');
            if (done) {
                done(err, null);
            }
        });
            */
        }

        private async Task<bool> IsActive()
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = null;
                string url = $"http://{Config.IpAddr}:8001{(Config.Port == 55000 ? "/ms/1.0/" : "/api/v2/")}"; // `http://${this.IP}:8001${this.PORT === 55000 ? '/ms/1.0/' : '/api/v2/'}`
                client.Timeout = TimeSpan.FromSeconds(2);
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                try
                {
                    response = await client.GetAsync(url);
                }
                catch (WebException ex)
                {
                    // handle web exception
                    return false;
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.CancellationToken == tokenSource.Token)
                    {
                        // a real cancellation, triggered by the caller
                        return false;
                    }
                    else
                    {
                        // a web request timeout (possibly other things!?)
                        return false;
                    }
                }
                string content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($" Response content: {content}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                return false;
            }
        }

        private string GetWsUrl()
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Config.AppName);
            string nameApp = System.Convert.ToBase64String(bytes);
            return $"{(Config.Port == 8001 ? "ws" : "wss")}://{Config.IpAddr}:{Config.Port}/api/v2/channels/samsung.remote.control?name={nameApp}{(!Config.Token.Equals(String.Empty) ? $"&token={Config.Token}" : "")}";
        }

        private void InitializeKeys()
        {
            keys = new Dictionary<string, string>();
            keys["KEY_0"] = "KEY_0";
            keys["KEY_1"] = "KEY_1";
            keys["KEY_2"] = "KEY_2";
            keys["KEY_3"] = "KEY_3";
            keys["KEY_4"] = "KEY_4";
            keys["KEY_5"] = "KEY_5";
            keys["KEY_6"] = "KEY_6";
            keys["KEY_7"] = "KEY_7";
            keys["KEY_8"] = "KEY_8";
            keys["KEY_9"] = "KEY_9";
            keys["KEY_11"] = "KEY_11";
            keys["KEY_12"] = "KEY_12";
            keys["KEY_16_9"] = "KEY_16_9";
            keys["KEY_4_3"] = "KEY_4_3";
            keys["KEY_3SPEED"] = "KEY_3SPEED";
            keys["KEY_AD"] = "KEY_AD";
            keys["KEY_ADDDEL"] = "KEY_ADDDEL";
            keys["KEY_ALT_MHP"] = "KEY_ALT_MHP";
            keys["KEY_AMBIENT"] = "KEY_AMBIENT";
            keys["KEY_ANGLE"] = "KEY_ANGLE";
            keys["KEY_ANTENA"] = "KEY_ANTENA";
            keys["KEY_ANYNET"] = "KEY_ANYNET";
            keys["KEY_ANYVIEW"] = "KEY_ANYVIEW";
            keys["KEY_APP_LIST"] = "KEY_APP_LIST";
            keys["KEY_ASPECT"] = "KEY_ASPECT";
            keys["KEY_AUTO_ARC_ANTENNA_AIR"] = "KEY_AUTO_ARC_ANTENNA_AIR";
            keys["KEY_AUTO_ARC_ANTENNA_CABLE"] = "KEY_AUTO_ARC_ANTENNA_CABLE";
            keys["KEY_AUTO_ARC_ANTENNA_SATELLITE"] = "KEY_AUTO_ARC_ANTENNA_SATELLITE";
            keys["KEY_AUTO_ARC_ANYNET_AUTO_START"] = "KEY_AUTO_ARC_ANYNET_AUTO_START";
            keys["KEY_AUTO_ARC_ANYNET_MODE_OK"] = "KEY_AUTO_ARC_ANYNET_MODE_OK";
            keys["KEY_AUTO_ARC_AUTOCOLOR_FAIL"] = "KEY_AUTO_ARC_AUTOCOLOR_FAIL";
            keys["KEY_AUTO_ARC_AUTOCOLOR_SUCCESS"] = "KEY_AUTO_ARC_AUTOCOLOR_SUCCESS";
            keys["KEY_AUTO_ARC_C_FORCE_AGING"] = "KEY_AUTO_ARC_C_FORCE_AGING";
            keys["KEY_AUTO_ARC_CAPTION_ENG"] = "KEY_AUTO_ARC_CAPTION_ENG";
            keys["KEY_AUTO_ARC_CAPTION_KOR"] = "KEY_AUTO_ARC_CAPTION_KOR";
            keys["KEY_AUTO_ARC_CAPTION_OFF"] = "KEY_AUTO_ARC_CAPTION_OFF";
            keys["KEY_AUTO_ARC_CAPTION_ON"] = "KEY_AUTO_ARC_CAPTION_ON";
            keys["KEY_AUTO_ARC_JACK_IDENT"] = "KEY_AUTO_ARC_JACK_IDENT";
            keys["KEY_AUTO_ARC_LNA_OFF"] = "KEY_AUTO_ARC_LNA_OFF";
            keys["KEY_AUTO_ARC_LNA_ON"] = "KEY_AUTO_ARC_LNA_ON";
            keys["KEY_AUTO_ARC_PIP_CH_CHANGE"] = "KEY_AUTO_ARC_PIP_CH_CHANGE";
            keys["KEY_AUTO_ARC_PIP_DOUBLE"] = "KEY_AUTO_ARC_PIP_DOUBLE";
            keys["KEY_AUTO_ARC_PIP_LARGE"] = "KEY_AUTO_ARC_PIP_LARGE";
            keys["KEY_AUTO_ARC_PIP_LEFT_BOTTOM"] = "KEY_AUTO_ARC_PIP_LEFT_BOTTOM";
            keys["KEY_AUTO_ARC_PIP_LEFT_TOP"] = "KEY_AUTO_ARC_PIP_LEFT_TOP";
            keys["KEY_AUTO_ARC_PIP_RIGHT_BOTTOM"] = "KEY_AUTO_ARC_PIP_RIGHT_BOTTOM";
            keys["KEY_AUTO_ARC_PIP_RIGHT_TOP"] = "KEY_AUTO_ARC_PIP_RIGHT_TOP";
            keys["KEY_AUTO_ARC_PIP_SMALL"] = "KEY_AUTO_ARC_PIP_SMALL";
            keys["KEY_AUTO_ARC_PIP_SOURCE_CHANGE"] = "KEY_AUTO_ARC_PIP_SOURCE_CHANGE";
            keys["KEY_AUTO_ARC_PIP_WIDE"] = "KEY_AUTO_ARC_PIP_WIDE";
            keys["KEY_AUTO_ARC_RESET"] = "KEY_AUTO_ARC_RESET";
            keys["KEY_AUTO_ARC_USBJACK_INSPECT"] = "KEY_AUTO_ARC_USBJACK_INSPECT";
            keys["KEY_AUTO_FORMAT"] = "KEY_AUTO_FORMAT";
            keys["KEY_AUTO_PROGRAM"] = "KEY_AUTO_PROGRAM";
            keys["KEY_AV1"] = "KEY_AV1";
            keys["KEY_AV2"] = "KEY_AV2";
            keys["KEY_AV3"] = "KEY_AV3";
            keys["KEY_BACK_MHP"] = "KEY_BACK_MHP";
            keys["KEY_BOOKMARK"] = "KEY_BOOKMARK";
            keys["KEY_CALLER_ID"] = "KEY_CALLER_ID";
            keys["KEY_CAPTION"] = "KEY_CAPTION";
            keys["KEY_CATV_MODE"] = "KEY_CATV_MODE";
            keys["KEY_CH_LIST"] = "KEY_CH_LIST";
            keys["KEY_CHDOWN"] = "KEY_CHDOWN";
            keys["KEY_CHUP"] = "KEY_CHUP";
            keys["KEY_CLEAR"] = "KEY_CLEAR";
            keys["KEY_CLOCK_DISPLAY"] = "KEY_CLOCK_DISPLAY";
            keys["KEY_COMPONENT1"] = "KEY_COMPONENT1";
            keys["KEY_COMPONENT2"] = "KEY_COMPONENT2";
            keys["KEY_CONTENTS"] = "KEY_CONTENTS";
            keys["KEY_CONVERGENCE"] = "KEY_CONVERGENCE";
            keys["KEY_CONVERT_AUDIO_MAINSUB"] = "KEY_CONVERT_AUDIO_MAINSUB";
            keys["KEY_CUSTOM"] = "KEY_CUSTOM";
            keys["KEY_CYAN"] = "KEY_CYAN";
            keys["KEY_DEVICE_CONNECT"] = "KEY_DEVICE_CONNECT";
            keys["KEY_DISC_MENU"] = "KEY_DISC_MENU";
            keys["KEY_DMA"] = "KEY_DMA";
            keys["KEY_DNET"] = "KEY_DNET";
            keys["KEY_DNIe"] = "KEY_DNIe";
            keys["KEY_DNSe"] = "KEY_DNSe";
            keys["KEY_DOOR"] = "KEY_DOOR";
            keys["KEY_DOWN"] = "KEY_DOWN";
            keys["KEY_DSS_MODE"] = "KEY_DSS_MODE";
            keys["KEY_DTV"] = "KEY_DTV";
            keys["KEY_DTV_LINK"] = "KEY_DTV_LINK";
            keys["KEY_DTV_SIGNAL"] = "KEY_DTV_SIGNAL";
            keys["KEY_DVD_MODE"] = "KEY_DVD_MODE";
            keys["KEY_DVI"] = "KEY_DVI";
            keys["KEY_DVR"] = "KEY_DVR";
            keys["KEY_DVR_MENU"] = "KEY_DVR_MENU";
            keys["KEY_DYNAMIC"] = "KEY_DYNAMIC";
            keys["KEY_ENTER"] = "KEY_ENTER";
            keys["KEY_ENTERTAINMENT"] = "KEY_ENTERTAINMENT";
            keys["KEY_ESAVING"] = "KEY_ESAVING";
            keys["KEY_EXIT"] = "KEY_EXIT";
            keys["KEY_EXT1"] = "KEY_EXT1";
            keys["KEY_EXT10"] = "KEY_EXT10";
            keys["KEY_EXT11"] = "KEY_EXT11";
            keys["KEY_EXT12"] = "KEY_EXT12";
            keys["KEY_EXT13"] = "KEY_EXT13";
            keys["KEY_EXT14"] = "KEY_EXT14";
            keys["KEY_EXT15"] = "KEY_EXT15";
            keys["KEY_EXT16"] = "KEY_EXT16";
            keys["KEY_EXT17"] = "KEY_EXT17";
            keys["KEY_EXT18"] = "KEY_EXT18";
            keys["KEY_EXT19"] = "KEY_EXT19";
            keys["KEY_EXT2"] = "KEY_EXT2";
            keys["KEY_EXT20"] = "KEY_EXT20";
            keys["KEY_EXT21"] = "KEY_EXT21";
            keys["KEY_EXT22"] = "KEY_EXT22";
            keys["KEY_EXT23"] = "KEY_EXT23";
            keys["KEY_EXT24"] = "KEY_EXT24";
            keys["KEY_EXT25"] = "KEY_EXT25";
            keys["KEY_EXT26"] = "KEY_EXT26";
            keys["KEY_EXT27"] = "KEY_EXT27";
            keys["KEY_EXT28"] = "KEY_EXT28";
            keys["KEY_EXT29"] = "KEY_EXT29";
            keys["KEY_EXT3"] = "KEY_EXT3";
            keys["KEY_EXT30"] = "KEY_EXT30";
            keys["KEY_EXT31"] = "KEY_EXT31";
            keys["KEY_EXT32"] = "KEY_EXT32";
            keys["KEY_EXT33"] = "KEY_EXT33";
            keys["KEY_EXT34"] = "KEY_EXT34";
            keys["KEY_EXT35"] = "KEY_EXT35";
            keys["KEY_EXT36"] = "KEY_EXT36";
            keys["KEY_EXT37"] = "KEY_EXT37";
            keys["KEY_EXT38"] = "KEY_EXT38";
            keys["KEY_EXT39"] = "KEY_EXT39";
            keys["KEY_EXT4"] = "KEY_EXT4";
            keys["KEY_EXT40"] = "KEY_EXT40";
            keys["KEY_EXT41"] = "KEY_EXT41";
            keys["KEY_EXT5"] = "KEY_EXT5";
            keys["KEY_EXT6"] = "KEY_EXT6";
            keys["KEY_EXT7"] = "KEY_EXT7";
            keys["KEY_EXT8"] = "KEY_EXT8";
            keys["KEY_EXT9"] = "KEY_EXT9";
            keys["KEY_FACTORY"] = "KEY_FACTORY";
            keys["KEY_FAVCH"] = "KEY_FAVCH";
            keys["KEY_FF"] = "KEY_FF";
            keys["KEY_FF_"] = "KEY_FF_";
            keys["KEY_FM_RADIO"] = "KEY_FM_RADIO";
            keys["KEY_GAME"] = "KEY_GAME";
            keys["KEY_GREEN"] = "KEY_GREEN";
            keys["KEY_GUIDE"] = "KEY_GUIDE";
            keys["KEY_HDMI"] = "KEY_HDMI";
            keys["KEY_HDMI1"] = "KEY_HDMI1";
            keys["KEY_HDMI2"] = "KEY_HDMI2";
            keys["KEY_HDMI3"] = "KEY_HDMI3";
            keys["KEY_HDMI4"] = "KEY_HDMI4";
            keys["KEY_HELP"] = "KEY_HELP";
            keys["KEY_HOME"] = "KEY_HOME";
            keys["KEY_ID_INPUT"] = "KEY_ID_INPUT";
            keys["KEY_ID_SETUP"] = "KEY_ID_SETUP";
            keys["KEY_INFO"] = "KEY_INFO";
            keys["KEY_INSTANT_REPLAY"] = "KEY_INSTANT_REPLAY";
            keys["KEY_LEFT"] = "KEY_LEFT";
            keys["KEY_LINK"] = "KEY_LINK";
            keys["KEY_LIVE"] = "KEY_LIVE";
            keys["KEY_MAGIC_BRIGHT"] = "KEY_MAGIC_BRIGHT";
            keys["KEY_MAGIC_CHANNEL"] = "KEY_MAGIC_CHANNEL";
            keys["KEY_MDC"] = "KEY_MDC";
            keys["KEY_MENU"] = "KEY_MENU";
            keys["KEY_MIC"] = "KEY_MIC";
            keys["KEY_MORE"] = "KEY_MORE";
            keys["KEY_MOVIE1"] = "KEY_MOVIE1";
            keys["KEY_MS"] = "KEY_MS";
            keys["KEY_MTS"] = "KEY_MTS";
            keys["KEY_MUTE"] = "KEY_MUTE";
            keys["KEY_NINE_SEPERATE"] = "KEY_NINE_SEPERATE";
            keys["KEY_OPEN"] = "KEY_OPEN";
            keys["KEY_PANNEL_CHDOWN"] = "KEY_PANNEL_CHDOWN";
            keys["KEY_PANNEL_CHUP"] = "KEY_PANNEL_CHUP";
            keys["KEY_PANNEL_ENTER"] = "KEY_PANNEL_ENTER";
            keys["KEY_PANNEL_MENU"] = "KEY_PANNEL_MENU";
            keys["KEY_PANNEL_POWER"] = "KEY_PANNEL_POWER";
            keys["KEY_PANNEL_SOURCE"] = "KEY_PANNEL_SOURCE";
            keys["KEY_PANNEL_VOLDOW"] = "KEY_PANNEL_VOLDOW";
            keys["KEY_PANNEL_VOLUP"] = "KEY_PANNEL_VOLUP";
            keys["KEY_PANORAMA"] = "KEY_PANORAMA";
            keys["KEY_PAUSE"] = "KEY_PAUSE";
            keys["KEY_PCMODE"] = "KEY_PCMODE";
            keys["KEY_PERPECT_FOCUS"] = "KEY_PERPECT_FOCUS";
            keys["KEY_PICTURE_SIZE"] = "KEY_PICTURE_SIZE";
            keys["KEY_PIP_CHDOWN"] = "KEY_PIP_CHDOWN";
            keys["KEY_PIP_CHUP"] = "KEY_PIP_CHUP";
            keys["KEY_PIP_ONOFF"] = "KEY_PIP_ONOFF";
            keys["KEY_PIP_SCAN"] = "KEY_PIP_SCAN";
            keys["KEY_PIP_SIZE"] = "KEY_PIP_SIZE";
            keys["KEY_PIP_SWAP"] = "KEY_PIP_SWAP";
            keys["KEY_PLAY"] = "KEY_PLAY";
            keys["KEY_PLUS100"] = "KEY_PLUS100";
            keys["KEY_PMODE"] = "KEY_PMODE";
            keys["KEY_POWER"] = "KEY_POWER";
            keys["KEY_POWEROFF"] = "KEY_POWEROFF";
            keys["KEY_POWERON"] = "KEY_POWERON";
            keys["KEY_PRECH"] = "KEY_PRECH";
            keys["KEY_PRINT"] = "KEY_PRINT";
            keys["KEY_PROGRAM"] = "KEY_PROGRAM";
            keys["KEY_QUICK_REPLAY"] = "KEY_QUICK_REPLAY";
            keys["KEY_REC"] = "KEY_REC";
            keys["KEY_RED"] = "KEY_RED";
            keys["KEY_REPEAT"] = "KEY_REPEAT";
            keys["KEY_RESERVED1"] = "KEY_RESERVED1";
            keys["KEY_RETURN"] = "KEY_RETURN";
            keys["KEY_REWIND"] = "KEY_REWIND";
            keys["KEY_REWIND_"] = "KEY_REWIND_";
            keys["KEY_RIGHT"] = "KEY_RIGHT";
            keys["KEY_RSS"] = "KEY_RSS";
            keys["KEY_RSURF"] = "KEY_RSURF";
            keys["KEY_SCALE"] = "KEY_SCALE";
            keys["KEY_SEFFECT"] = "KEY_SEFFECT";
            keys["KEY_SETUP_CLOCK_TIMER"] = "KEY_SETUP_CLOCK_TIMER";
            keys["KEY_SLEEP"] = "KEY_SLEEP";
            keys["KEY_SOURCE"] = "KEY_SOURCE";
            keys["KEY_SRS"] = "KEY_SRS";
            keys["KEY_STANDARD"] = "KEY_STANDARD";
            keys["KEY_STB_MODE"] = "KEY_STB_MODE";
            keys["KEY_STILL_PICTURE"] = "KEY_STILL_PICTURE";
            keys["KEY_STOP"] = "KEY_STOP";
            keys["KEY_SUB_TITLE"] = "KEY_SUB_TITLE";
            keys["KEY_SVIDEO1"] = "KEY_SVIDEO1";
            keys["KEY_SVIDEO2"] = "KEY_SVIDEO2";
            keys["KEY_SVIDEO3"] = "KEY_SVIDEO3";
            keys["KEY_TOOLS"] = "KEY_TOOLS";
            keys["KEY_TOPMENU"] = "KEY_TOPMENU";
            keys["KEY_TTX_MIX"] = "KEY_TTX_MIX";
            keys["KEY_TTX_SUBFACE"] = "KEY_TTX_SUBFACE";
            keys["KEY_TURBO"] = "KEY_TURBO";
            keys["KEY_TV"] = "KEY_TV";
            keys["KEY_TV_MODE"] = "KEY_TV_MODE";
            keys["KEY_UP"] = "KEY_UP";
            keys["KEY_VCHIP"] = "KEY_VCHIP";
            keys["KEY_VCR_MODE"] = "KEY_VCR_MODE";
            keys["KEY_VOLDOWN"] = "KEY_VOLDOWN";
            keys["KEY_VOLUP"] = "KEY_VOLUP";
            keys["KEY_W_LINK"] = "KEY_W_LINK";
            keys["KEY_WHEEL_LEFT"] = "KEY_WHEEL_LEFT";
            keys["KEY_WHEEL_RIGHT"] = "KEY_WHEEL_RIGHT";
            keys["KEY_YELLOW"] = "KEY_YELLOW";
            keys["KEY_ZOOM_IN"] = "KEY_ZOOM_IN";
            keys["KEY_ZOOM_MOVE"] = "KEY_ZOOM_MOVE";
            keys["KEY_ZOOM_OUT"] = "KEY_ZOOM_OUT";
            keys["KEY_ZOOM1"] = "KEY_ZOOM1";
            keys["KEY_ZOOM2"] = "KEY_ZOOM2";
        }
    }
}
