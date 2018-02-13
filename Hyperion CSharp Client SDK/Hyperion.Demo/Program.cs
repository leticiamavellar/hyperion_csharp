using MicronOptics.Hyperion.Communication;
using MicronOptics.Hyperion.Communication.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MicronOptics.Hyperion.Demo
{
	class Program
	{
		static void Main(string[] args)
		{
			// *** CHANGE TO THE IP ADDRESS FOR INSTRUMENT ***
			string instrumentIpAddress = args.Length > 0 ? args[0] : "10.0.0.55";

			// Create a TCP client for communicating with the Hyperion instrument
			TcpClient tcpClient = new TcpClient();

			// Connect to the instrument over TCP/IP

			tcpClient.Connect(instrumentIpAddress, Command.TcpPort);
			NetworkStream tcpNetworkStream = tcpClient.GetStream();


			// Execute a simple command to retrieve the instrument serial number
			CommandResponse response = Command.Execute(
				tcpNetworkStream,
				CommandName.GetSerialNumber);

			// The response (unless specifically suppressed) contains an ASCII based Message field
			// AND a binary (byte[]) Content field. The ASCII field is intended to be human readable
			// and the binary data is intended to be easily parsed by a computer.
			Console.WriteLine("Instrument Serial Number");
			Console.WriteLine("------------------------");
			Console.WriteLine($"Message: {response.Message}");
			Console.WriteLine($"Content Converted to String {response.AsString()}");

			Console.WriteLine();
			Console.WriteLine();

			// Execute the #GetPeaks command. The CommandName contains static string members
			// for commands exposed by the instrument.
			response = Command.Execute(
				tcpNetworkStream,
				CommandName.GetPeaks);

			// Several extension methods are defined in the communication library that 
			// easily convert the response from a byte[] to useful types such as
			// int, double, and more complex responses such as Peak/Spectrum data
			PeakData peakData = response.AsPeakData();

			Console.WriteLine("Peak Data Header");
			Console.WriteLine("---------------------");

			Console.WriteLine($"Serial Number: {peakData.SerialNumber}");
			Console.WriteLine($"Timestamp: {peakData.Timestamp}");

			Console.WriteLine();

			Console.WriteLine("Peaks");
			Console.WriteLine("-----");

			// The PeakData exposes the peaks in two ways...as Arrays and as Enumerables. The
			// arrays will allocate new memory for the data but can be advantageous if the 
			// data needs to be repeatedly accessed. The Enumerable is great for situations where
			// the data needs to be simply iterated through once and processed. This provides very
			// high performance and low overhead for situations such as streaming large number of
			// peaks at very high speeds.
			for (int channelIndex = 0; channelIndex < 4; channelIndex++)
			{
				Console.WriteLine($"Channel {channelIndex}: ");

				foreach (double wavelength in peakData.AsEnumerable(1))
				{
					Console.WriteLine($"\t{wavelength} nm");
				}
			}

			Console.WriteLine();
			Console.WriteLine();



			// Retrieve the optical full spectrum response
			response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.GetSpectrum);

			// Use the extension methods to easily obtain the spectrum data
			SpectrumData spectrumData = response.AsSpectrumData();

			Console.WriteLine("Full Spectrum");
			Console.WriteLine("-------------");
			Console.WriteLine($"Wavelength Start: {spectrumData.WavelengthStart:F3} nm");
			Console.WriteLine($"Wavelength Step: {(int)(1000 * spectrumData.WavelengthStep)} pm");
			Console.WriteLine($"Wavelength Step Count: {spectrumData.WavelengthStepCount}");
			Console.WriteLine($"Channel Count: {spectrumData.ChannelCount}");
			Console.WriteLine($"Serial Number: {spectrumData.SerialNumber}");
			Console.WriteLine($"Timestamp: {spectrumData.Timestamp}");

			Console.WriteLine();
			Console.WriteLine();



			// Retrieve the defined Sensors
			response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.ExportSensors);

			Console.WriteLine(response.Message);

			int offset = 0;

			int version = BitConverter.ToUInt16(response.Content, offset);
			offset += sizeof(UInt16);

			int numberOfSensors = BitConverter.ToUInt16(response.Content, offset);
			offset += sizeof(UInt16);

			// Sensor Count
			Console.WriteLine($"Sensors - {numberOfSensors} (Data Export Version = {version})");
			Console.WriteLine();

			// Create sensors

			for (int sensorIndex = 0; sensorIndex < numberOfSensors; sensorIndex++)
			{
				FabryPerotAccelerometer fpSensor = (FabryPerotAccelerometer)SensorBase.Create(response.Content, ref offset);

				Console.WriteLine($"Sensor {sensorIndex + 1} - {fpSensor.Name} ({fpSensor.Model})");
				Console.WriteLine("----------------------------------------");
				Console.WriteLine($"ID: {fpSensor.Id}");
				Console.WriteLine($"Model: {fpSensor.Model}");
				Console.WriteLine($"Channel: {fpSensor.DutChannelIndex + 1}");
				Console.WriteLine($"Wavelength Band: {fpSensor.WavelengthBand} nm");
				Console.WriteLine($"Calibration Factor: {fpSensor.CalibrationFactor} m");
				Console.WriteLine($"Delta-N Over Range Threshold: {fpSensor.DnFromPeakOverRangeThreshold}");
				Console.WriteLine($"Delta-N Peak Threshold: {fpSensor.DnFromPeakThreshold}");
				Console.WriteLine($"Recenter Threshold Low: {fpSensor.RecenterThresholdLow} nm^2");
				Console.WriteLine();
			}

			Console.WriteLine();



			// Cleanup by closing the TCP connection
			tcpNetworkStream.Close();
			tcpClient.Close();




			// Now demonstrating using the data streaming to continuously read consecutive
			// peak data sets.
			tcpClient = new TcpClient();
			tcpClient.Connect(instrumentIpAddress, StreamingDataReader.PeakTcpPort);
			tcpNetworkStream = tcpClient.GetStream();

			// The StreamingDataReader class works for peaks and full spectrum. It can be
			// used to easily and efficiently read consecutive datasets. The class internally
			// uses a single buffer of memory to avoid allocating and collecting large
			// amounts of memory and system resources. The mode Peak, Spectrum, Sensor) is
			// defined when the reader is created.
			StreamingDataReader reader = new StreamingDataReader(StreamingDataMode.Peaks);

			Console.WriteLine("Streaming Peak Data Acquistion Serial Numbers");
			Console.WriteLine("---------------------------------------------");

			for (int index = 0; index < 10; index++)
			{
				Console.WriteLine(
					$"{index}: " +
					$"{ reader.ReadStreamingData(tcpNetworkStream).AsPeakData().SerialNumber}");
			}

			Console.WriteLine();

			tcpNetworkStream.Close();
			tcpClient.Close();


			// Wait for any key press to exit
			Console.ReadLine();
		}
	}
}

