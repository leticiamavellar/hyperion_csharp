using System.Net.Sockets;
using static System.Console;
using MicronOptics.Hyperion.Communication;
using MicronOptics.Hyperion.Communication.Sensors;
using System;

namespace MicronOptics.Hyperion.Console
{
	class Program
	{
		static void Main(string[] args)
		{
			// *** CHANGE TO THE IP ADDRESS FOR INSTRUMENT ***
			string instrumentIpAddress = "192.168.86.70";

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
			WriteLine("Instrument Serial Number");
			WriteLine("------------------------");
			WriteLine($"Message: {response.Message}");
			WriteLine($"Content Converted to String {response.AsString()}");

			WriteLine();
			WriteLine();

			// Execute the #GetPeaks command. The CommandName contains static string members
			// for commands exposed by the instrument.
			response = Command.Execute(
				tcpNetworkStream,
				CommandName.GetPeaks);

			// Several extension methods are defined in the communication library that 
			// easily convert the response from a byte[] to useful types such as
			// int, double, and more complex responses such as Peak/Spectrum data
			PeakData peakData = response.AsPeakData();

			WriteLine("Peak Data Header");
			WriteLine("---------------------");

			WriteLine($"Serial Number: {peakData.SerialNumber}");
			WriteLine($"Timestamp: {peakData.Timestamp}");

			WriteLine();

			WriteLine("Peaks");
			WriteLine("-----");

			// The PeakData exposes the peaks in two ways...as Arrays and as Enumerables. The
			// arrays will allocate new memory for the data but can be advantageous if the 
			// data needs to be repeatedly accessed. The Enumerable is great for situations where
			// the data needs to be simply iterated through once and processed. This provides very
			// high performance and low overhead for situations such as streaming large number of
			// peaks at very high speeds.
			for (int channelIndex = 0; channelIndex < 4; channelIndex++)
			{
				WriteLine($"Channel {channelIndex}: ");

				foreach (double wavelength in peakData.AsEnumerable(1))
				{
					WriteLine($"\t{wavelength} nm");
				}
			}

			WriteLine();
			WriteLine();



			// Retrieve the optical full spectrum response
			response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.GetSpectrum);

			// Use the extension methods to easily obtain the spectrum data
			SpectrumData spectrumData = response.AsSpectrumData();

			WriteLine("Full Spectrum");
			WriteLine("-------------");
			WriteLine($"Wavelength Start: {spectrumData.WavelengthStart:F3} nm");
			WriteLine($"Wavelength Step: {(int)(1000 * spectrumData.WavelengthStep)} pm");
			WriteLine($"Wavelength Step Count: {spectrumData.WavelengthStepCount}");
			WriteLine($"Channel Count: {spectrumData.ChannelCount}");
			WriteLine($"Serial Number: {spectrumData.SerialNumber}");
			WriteLine($"Timestamp: {spectrumData.Timestamp}");

			WriteLine();
			WriteLine();

			// Remove existing sensors
			response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.GetSensorNames);
			string[] sensorNames = response.AsString().Split(' ');
			foreach (string sensorName in sensorNames)
			{
				WriteLine($"Removing sensor: {sensorName}");
				response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.RemoveSensor, sensorName);
			}

			// Add some sensors
			for (int i = 1; i <= 20; i++)
			{
				string name = $"sensor_{i}";
				string model = i % 2 == 1 ? "os7510" : "os7520";
				int channel = (i - 1) % 4 + 1;
				int wavelength = 1510 + ((i - 1) % 4) * 20;
				bool fixedOrientation = i % 2 == 0;
				double calibration = 10.0 * i;
				WriteLine($"Adding Sensor {name}");
				WriteLine("----------------------------------------");
				WriteLine($"Model: {model}");
				WriteLine($"Channel: {channel}");
				WriteLine($"Wavelength Band: {wavelength} nm");
				WriteLine($"Calibration Factor: {calibration} nm/g");
				WriteLine($"Fixed Orientation: {fixedOrientation}");
				WriteLine();
				string[] input_args = {name,
					model,
					channel.ToString(),
					"0",
					wavelength.ToString(),
					calibration.ToString(),
					fixedOrientation.ToString()};
				response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.AddSensor, input_args);
			}

			// Retrieve the defined Sensors
			response = Command.Execute(tcpNetworkStream, CommandOptions.None, CommandName.ExportSensors);

			WriteLine(response.Message);

			int offset = 0;

			int version = BitConverter.ToUInt16(response.Content, offset);
			offset += sizeof(UInt16);

			int numberOfSensors = BitConverter.ToUInt16(response.Content, offset);
			offset += sizeof(UInt16);

			// Sensor Count
			WriteLine($"Sensors - {numberOfSensors} (Data Export Version = {version})");
			WriteLine();

			// Create sensors

			for (int sensorIndex = 0; sensorIndex < numberOfSensors; sensorIndex++)
			{
				FabryPerotAccelerometer fpSensor = (FabryPerotAccelerometer)SensorBase.Create(response.Content, ref offset);
				WriteLine($"Sensor {sensorIndex + 1} - {fpSensor.Name} ({fpSensor.Model})");
				WriteLine($"Sensor Definition Version: {fpSensor.FPSesnorVersion}");
				WriteLine("----------------------------------------");
				WriteLine($"ID: {fpSensor.Id}");
				WriteLine($"Model: {fpSensor.Model}");
				WriteLine($"Channel: {fpSensor.DutChannelIndex + 1}");
				WriteLine($"Wavelength Band: {fpSensor.WavelengthBand} nm");
				WriteLine($"Calibration Factor: {fpSensor.CalibrationFactor} nm/g");
				WriteLine($"Fixed Orientation: {fpSensor.FixedOrientation}");
				WriteLine();
			}

			WriteLine();



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

			WriteLine("Streaming Peak Data Acquistion Serial Numbers");
			WriteLine("---------------------------------------------");

			for (int index = 0; index < 10; index++)
			{
				WriteLine(
					$"{index}: " +
					$"{ reader.ReadStreamingData(tcpNetworkStream).AsPeakData().SerialNumber}");
			}

			WriteLine();

			tcpNetworkStream.Close();
			tcpClient.Close();



			// Now demonstrating using the data streaming to continuously read consecutive
			// sensor data sets.
			tcpClient = new TcpClient();
			tcpClient.Connect(instrumentIpAddress, StreamingDataReader.SensorTcpPort);
			tcpNetworkStream = tcpClient.GetStream();

			// The StreamingDataReader class works for peaks, full spectrum and sensors. It can be
			// used to easily and efficiently read consecutive datasets. The class internally
			// uses a single buffer of memory to avoid allocating and collecting large
			// amounts of memory and system resources. The mode Peak, Spectrum, Sensor) is
			// defined when the reader is created.
			StreamingDataReader sensorReader = new StreamingDataReader(StreamingDataMode.Sensor);

			WriteLine("Streaming Sensor Data Acquistion Serial Numbers");
			WriteLine("---------------------------------------------");

			for (int index = 0; index < 10; index++)
			{
				SensorData sensorData = sensorReader.ReadStreamingData(tcpNetworkStream).AsSensorData();

				WriteLine(
					$"{index}: " +
					$"{sensorData.SerialNumber}");

			}

			WriteLine();

			tcpNetworkStream.Close();
			tcpClient.Close();




			// Wait for any key press to exit
			ReadLine();
		}
	}
}
