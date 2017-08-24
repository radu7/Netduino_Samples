﻿using System;
using System.Threading;
using System.IO.Ports;
using Microsoft.SPOT;
using System.Text;

namespace UARTTest
{
    public class Program
    {
		/// <summary>
		/// Two com ports, one sender and one receiver.
		/// </summary>
		/// <remarks>
		/// Note that the transmitter and the receiver must be configured to use the
		/// same boud rate, number of bits etc.
		/// </remarks>
		static SerialPort transmitter = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
		static SerialPort receiver = new SerialPort("COM4", 9600, Parity.None, 8, StopBits.One);

		/// <summary>
		/// Timer object generates and event periodically to transmit data to the receiver.
		/// </summary>
        static Timer timer = new Timer(Timer_Interrupt, null, 0, 2000);

		/// <summary>
		/// Variables to hold information about the messages being transmitted and received.
		/// </summary>
        static int count = 0;
		static string messageBeingReceived = "";

		/// <summary>
		/// The entry point of the program, where the program control starts and ends.
		/// </summary>
		public static void Main()
        {
            transmitter.Open();
            receiver.Open();
			receiver.DataReceived += SerialDataReceived;
			Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
		/// Process data from the serial port(s)
        /// </summary>
        /// <param name="sender">Serial port that is receiving the data.</param>
        /// <param name="e">Event information.</param>
        static void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
			if ((e.EventType == SerialData.Chars) && (sender == receiver))
			{
				const int BUFFER_SIZE = 1024;
				byte[] buffer = new byte[BUFFER_SIZE];

				int amount = ((SerialPort)sender).Read(buffer, 0, BUFFER_SIZE);
				if (amount > 0)
				{
					char[] characters = Encoding.UTF8.GetChars(buffer);
					for (int index = 0; index < amount; index++)
					{
						if (buffer[index] == '\n')
						{
							Debug.Print("Message received: " + messageBeingReceived);
							messageBeingReceived = "";
						}
						else
						{
							messageBeingReceived += characters[index];
						}
					}
				}
			}
		}

        /// <summary>
        /// Periodic interrupt generated by the timer.
        /// </summary>
        /// <param name="state">State.</param>
        static void Timer_Interrupt(object state)
        {
            if (transmitter.IsOpen)
            {
                count++;
                String messageToSend = count.ToString();
                Debug.Print("Sending message: " + messageToSend);
                messageToSend += "\n";
                transmitter.Write(Encoding.UTF8.GetBytes(messageToSend), 0, messageToSend.Length);
            }
        }
    }
}
