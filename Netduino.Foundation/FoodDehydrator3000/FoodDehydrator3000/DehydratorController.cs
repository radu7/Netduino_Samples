using System;
using System.Threading;
using Microsoft.SPOT;
using H = Microsoft.SPOT.Hardware;
using N = SecretLabs.NETMF.Hardware.Netduino;
using Netduino.Foundation.Sensors.Temperature;
using Netduino.Foundation.Relays;
using Netduino.Foundation.Sensors.Buttons;
using Netduino.Foundation.Generators;
using Netduino.Foundation.Displays;
using Netduino.Foundation.Controllers.PID;

namespace FoodDehydrator3000
{
    public class DehydratorController
    {
        // events
        public event EventHandler RunTimeElapsed = delegate { };
        public event EventHandler CooldownElapsed = delegate { };

        // peripherals
        protected AnalogTemperature _tempSensor = null;
        protected SoftPwm _heaterRelayPwm = null;
        protected Relay _fanRelay = null;
        protected ITextDisplay _display = null;

        // controllers
        IPidController _pidController = null;

        // other members
        Thread _tempControlThread = null;
        int _powerUpdateInterval = 2000; // milliseconds; how often to update the power

        // properties
        public bool Running {
            get { return _running; }
        }
        protected bool _running = false;

        public float TargetTemperature { get; set; }

        public TimeSpan RunningTimeLeft
        {
            get
            {
                if(_isTimerSet && _startTime != DateTime.MinValue)
                {
                    return _timerStartValue - ((TimeSpan)(DateTime.Now - _startTime));
                }
                else
                {
                    return TimeSpan.Zero;
                }
            }
        }

        protected bool _isTimerSet = false;
        protected TimeSpan _timerStartValue = TimeSpan.Zero;
        protected DateTime _startTime = DateTime.MinValue;

        public DehydratorController(AnalogTemperature tempSensor, SoftPwm heater, Relay fan, ITextDisplay display)
        {
            _tempSensor = tempSensor;
            _heaterRelayPwm = heater;
            _fanRelay = fan;
            _display = display;

            _pidController = new StandardPidController();
            _pidController.ProportionalComponent = .5f; // proportional
            _pidController.IntegralComponent = .55f; // integral time minutes
            _pidController.DerivativeComponent = 0f; // derivative time in minutes
            _pidController.OutputMin = 0.0f; // 0% power minimum
            _pidController.OutputMax = 1.0f; // 100% power max
            _pidController.OutputTuningInformation = false;

        }

        public void TurnOn(float temp)
        {
            TurnOn(temp, TimeSpan.Zero);
        }

        public void TurnOn(float temp, TimeSpan runningTime)
        {
            // set our state vars
            TargetTemperature = (float)temp;
            Debug.Print("Turning on.");
            this._timerStartValue = runningTime;
            this._startTime = DateTime.Now;
            this._isTimerSet = _timerStartValue != TimeSpan.Zero;
            this._running = true;

            // keeping fan off, to get temp to rise.
            this._fanRelay.IsOn = true;
            
            // TEMP - to be replaced with PID stuff
            this._heaterRelayPwm.Frequency = 1.0f / 5.0f; // 5 seconds to start (later we can slow down)
            // on start, if we're under temp, turn on the heat to start.
            float duty = (_tempSensor.Temperature < TargetTemperature) ? 1.0f : 0.0f;
            this._heaterRelayPwm.DutyCycle = duty;
            this._heaterRelayPwm.Start();

            // start our temp regulation thread. might want to change this to notify.
            StartRegulatingTemperatureThread();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay">in seconds</param>
        public void TurnOff(int delay)
        {
            Debug.Print("Turning off.");
            this._heaterRelayPwm.Stop();
            this._running = false;
            this._timerStartValue = TimeSpan.Zero;
            this.Cooldown(delay);
        }

        /// <summary>
        /// Runs the fan for a specified cooldown period.
        /// When the cooldown period elapses, it checks to see
        /// that the dehydrator hasn't been turned back on before
        /// turning fan off.
        /// </summary>
        /// <param name="delay"></param>
        protected void Cooldown(int delay)
        {
            Thread th = new Thread(() =>
            {
                Debug.Print("Cooldown delay, " + delay.ToString() + "secs");
                Thread.Sleep(delay * 1000);
                if (!this._running)
                {
                    Debug.Print("Cooldown elapsed, turning fan off.");
                    this._fanRelay.IsOn = false;
                    this.CooldownElapsed(this, new EventArgs());
                }
            });
            th.Start();
        }

        protected void StartRegulatingTemperatureThread()
        {
            _tempControlThread = new Thread(() => {

                // reset our integral history
                _pidController.ResetIntegrator();

                while (this._running) {

                    // set our input and target on the PID calculator
                    _pidController.ActualInput = _tempSensor.Temperature;
                    _pidController.TargetInput = this.TargetTemperature;

                    // get the appropriate power level (only use PI, since the temp signal is noisy)
                    var powerLevel = _pidController.CalculateControlOutput();
                    //Debug.Print("Temp: " + _tempSensor.Temperature.ToString() + "/" + TargetTemperature.ToString("N0") + "�C");

                    // set our PWM appropriately
                    //Debug.Print("Setting duty cycle to: " + (powerLevel * 100).ToString("N0") + "%");

                    this._heaterRelayPwm.DutyCycle = powerLevel;

                    // sleep for a while. 
                    Thread.Sleep(_powerUpdateInterval);
                }
            });
            _tempControlThread.Start();
        }
    }
}
