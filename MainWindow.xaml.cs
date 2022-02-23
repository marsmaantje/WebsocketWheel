using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WebSocketSharp;
using WebSocketSharp.Server;
using Windows.Gaming.Input;
using Windows.Gaming.Input.ForceFeedback;

namespace SteeringWheel_Interface
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WebSocketServer wssv; //the server object
        string message = ""; //last broadcasted message
        RacingWheel wheel; //current racingWheel
        RacingWheelReading state; //state of the racingWheel, to avoid reading it multiple times per update
        public float desiredPosition = 0; //position the wheel wants to be in
        ConstantForceEffect currentEffect = new ConstantForceEffect();

        public class websocketWheel : WebSocketBehavior
        {

            /// <summary>
            /// Process incoming websocket message
            /// </summary>
            /// <param name="e">MessaveEvent</param>
            protected override void OnMessage(MessageEventArgs e)
            {
                float num;
                if (float.TryParse(e.Data, out num) && Math.Abs(num) <= 1)
                {
                    Globals.currentWindow.desiredPosition = num;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Globals.currentWindow = this;


            //start a websocketServer
            wssv = new WebSocketServer("ws://localhost:4269");
            wssv.AddWebSocketService<websocketWheel>("/");
            wssv.Start();

            /*
            //start timer for websocket
            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += new EventHandler(serverBroadcast);
            timer.Interval = TimeSpan.FromSeconds(1 / 144f);
            timer.Start();

            //start timer for haptics updating
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(updateForceFeedback);
            timer.Interval = TimeSpan.FromSeconds(1 / 500f);
            timer.Start();
            */

            //start Timer for data polling
            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += new EventHandler(Update);
            timer.Interval = TimeSpan.FromSeconds(0);
            timer.Start();
            
            //start Timer for refreshing the haptics
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(loadEffect);
            timer.Interval = TimeSpan.FromSeconds(60);
            timer.Start();

            Update();
            if(wheel != null)
                loadEffect();


        }

        /// <summary>
        /// updates the visuals and broadcasts wheel information if anything changed
        /// </summary>
        public void Update(object sender = null, EventArgs e = null)
        {
            //only update everything if there is a wheel connected
            if (RacingWheel.RacingWheels.Count > 0)
            {
                //update wheel and state
                wheel = RacingWheel.RacingWheels[0];
                state = wheel.GetCurrentReading();

                //update everything else
                updateVisuals();

                serverBroadcast();
                updateForceFeedback();
            }
        }

        /// <summary>
        /// Updates the visual GUI to represent the current state of the racingWheel
        /// </summary>
        void updateVisuals()
        {
            WheelLeft.Value = Math.Max(0, -state.Wheel);
            WheelRight.Value = Math.Max(0, state.Wheel);
            brakePedal.Value = state.Brake;
            gasPedal.Value = state.Throttle;
        }

        /// <summary>
        /// Broadcasts a formatted string if the state changed from the previous update
        /// </summary>
        void serverBroadcast(object sender = null, EventArgs e = null)
        {
            string newMessage = string.Format((char)1 + "{0:0.####};" + (char)2 + "{1:0.###};" + (char)3 + "{2:0.###};", state.Wheel, state.Brake, state.Throttle);
            if (newMessage != message)
            {
                message = newMessage;
                wssv.WebSocketServices.Broadcast(message);
            }
        }

        /// <summary>
        /// Updates the forceFeedback according to the class variables set with applyForceFeedback
        /// </summary>
        void updateForceFeedback(object sender = null, EventArgs e = null)
        {

            float positionDelta = (float)Math.Max(-1, Math.Min(1, (desiredPosition - state.Wheel) * 5));
            //drive the wheel with the parsed value (between -1 and 1 inclusive)
            currentEffect.SetParameters(new Vector3(positionDelta), TimeSpan.FromDays(31));
        }

        void loadEffect(object sender = null, EventArgs e = null)
        {
            loadEffectAsync();
        }

        async Task loadEffectAsync()
        {
            currentEffect = new ConstantForceEffect();
            currentEffect.SetParameters(new Vector3(0), TimeSpan.FromSeconds(5));
            wheel.WheelMotor.StopAllEffects();
            _ = await wheel.WheelMotor.LoadEffectAsync(currentEffect);
            currentEffect.Start();
        }

    }

    /// <summary>
    /// class for global variables
    /// USE AS SPARINGLY AS POSSIBLE
    /// </summary>
    public static class Globals
    {
        public static MainWindow currentWindow;
    }
}
