using System;
using System.Collections.Generic;
using System.Linq;


namespace MicronOptics.Hyperion.Communication.Sensors
{
	public class FabryPerotAccelerometer : SensorBase
	{
		#region -- Constants --

		private const int _FabryPerotConfigurationVersion = 2;

		private const int _MaximumNumberOfPeaksPerSensor = 100; // 20 nm span with 50 Ghz produces approximately 50 values...x2 for margin.

		private const double _ErrorAverageLength = 100;
		private const double _ErrorFilterNumerator = 1.0 / _ErrorAverageLength;
		private const double _ErrorFilterDenominator = 1.0 - _ErrorFilterNumerator;
		private const int _RecenterCountDown = 500;

		private const double _WavelengthBandHalfWidth = 7.5; // in nm

		private const double _DnFromPeakOverRangeThresholdDefault = 0.5;
		private const double _DnFromPeakThresholdDefault = 0.8;
		private const double _RecenterThresholdDefault = 1.0e-5;

		private const int _WavelengthBandFieldOffset = 0;
		private const int _CalibrationFactorFieldOffset = 1;

		private const int _DnFromPeakOverRangeThresholdOffset = 2;
		private const int _DnFromPeakThresholdOffset = 3;
		private const int _RecenterThresholdOffset = 4;

		#endregion

		#region -- Instance Variables --

		private readonly double _wavelengthBand;

		private readonly double _calibrationFactor;
		private readonly double _inverseCalibrationFactor; // avoid repeated divisions

		private double _dnFromPeakOverRangeThreshold = _DnFromPeakOverRangeThresholdDefault;
		private double _dnFromPeakThreshold = _DnFromPeakThresholdDefault;
		private double _recenterThresholdLow = _RecenterThresholdDefault;

		#endregion


		#region -- Constructors --

		private FabryPerotAccelerometer(Guid id, string name, string model, int dutChannelIndex, double distance, double wavelengthBand, double calibrationFactor,
			double dnFromPeakOverRangeThreshold, double dnFromPeakThreshold, double recenterThresholdLow) : base(id, name, model, dutChannelIndex, distance)
		{
			// Center wavelength and high/low boundaries
			_wavelengthBand = wavelengthBand;

			// Store the calibration factor and store the inverse to use in multiplication that computes the output
			// in engineering units (g). This avoids the need for repeated divisions
			_calibrationFactor = calibrationFactor;
			_inverseCalibrationFactor = 1.0 / calibrationFactor;

			// Gain
			_dnFromPeakOverRangeThreshold = dnFromPeakOverRangeThreshold;

			// Thresholds
			_dnFromPeakThreshold = dnFromPeakThreshold;
			_recenterThresholdLow = recenterThresholdLow;

			// Fabry-Perot Accelerometers need to be updated every cycle regardless of user request
			IsActive = true;
		}

		#endregion


		#region -- Public Properties --

		public double WavelengthBand => _wavelengthBand;

		public double CalibrationFactor => _calibrationFactor;
		public double DnFromPeakOverRangeThreshold => _dnFromPeakOverRangeThreshold;
		public double DnFromPeakThreshold => _dnFromPeakThreshold;
		public double RecenterThresholdLow => _recenterThresholdLow;

		#endregion


		#region -- Static Methods --

		internal static FabryPerotAccelerometer Create(Guid id, string name, string model, int dutChannelIndex, double distance,
			string[] fields)
		{
			// Wavelength Band
			double wavelengthBand = double.Parse(fields[_WavelengthBandFieldOffset]);

			// Calibration Factor
			double calibrationFactor = double.Parse(fields[_CalibrationFactorFieldOffset]);

			// Recenter Gain
			double dnFromPeakOverRangeThreshol = (fields.Length > _DnFromPeakOverRangeThresholdOffset) ?
				double.Parse(fields[_DnFromPeakOverRangeThresholdOffset]) :
				_DnFromPeakOverRangeThresholdDefault;

			// Recenter ThresholdHigh
			double dnFromPeakThreshold = (fields.Length > _DnFromPeakThresholdOffset) ?
				double.Parse(fields[_DnFromPeakThresholdOffset]) :
				_DnFromPeakThresholdDefault;

			// Recenter ThresholdHigh
			double recenterThreshold = (fields.Length > _RecenterThresholdOffset) ?
				double.Parse(fields[_RecenterThresholdOffset]) :
				_RecenterThresholdDefault;


			return new FabryPerotAccelerometer(id, name, model, dutChannelIndex, distance, wavelengthBand, calibrationFactor,
				dnFromPeakOverRangeThreshol, dnFromPeakThreshold, recenterThreshold);
		}

		internal static FabryPerotAccelerometer Create(Guid id, string name, string model, int dutChannelIndex, double distance,
			byte[] configurationBytes, ref int offset)
		{
			double dnFromPeakOverRangeThreshold = _DnFromPeakOverRangeThresholdDefault;
			double dnFromPeakThreshold = _DnFromPeakThresholdDefault;
			double recenterThresholdLow = _RecenterThresholdDefault;

			// First two bytes are the Fabry-Perot configuration version
			int fabryPerotConfigurationVersion = BitConverter.ToUInt16(configurationBytes, offset);
			offset += sizeof(UInt16);

			// Wavelength Band
			double wavelengthBand = BitConverter.ToDouble(configurationBytes, offset);
			offset += sizeof(double);

			// Calibration Factor
			double calibrationFactor = BitConverter.ToDouble(configurationBytes, offset);
			offset += sizeof(double);

			if (fabryPerotConfigurationVersion == 1)
			{
				// Version 1 had different internally tweakable parameters. If a version 1 is loaded, default
				// all of the new paramaters to defeault values
				return new FabryPerotAccelerometer(id, name, model, dutChannelIndex, distance, wavelengthBand, calibrationFactor,
					dnFromPeakOverRangeThreshold, dnFromPeakThreshold, recenterThresholdLow);
			}
			else if (fabryPerotConfigurationVersion == 2)
			{
				// Recenter Gain
				dnFromPeakOverRangeThreshold = BitConverter.ToDouble(configurationBytes, offset);
				offset += sizeof(double);

				// Recenter ThresholdHigh
				dnFromPeakThreshold = BitConverter.ToDouble(configurationBytes, offset);
				offset += sizeof(double);

				// Recenter ThresholdHigh
				recenterThresholdLow = BitConverter.ToDouble(configurationBytes, offset);
				offset += sizeof(double);

				return new FabryPerotAccelerometer(id, name, model, dutChannelIndex, distance, wavelengthBand, calibrationFactor,
					dnFromPeakOverRangeThreshold, dnFromPeakThreshold, recenterThresholdLow);
			}
			else
			{
				throw new Exception($"Configuration for Sensor '{name}' contains an unrecoginized version - {fabryPerotConfigurationVersion}");
			}
		}

		#endregion

		#region -- Protected Methods --

		protected override IEnumerable<byte> GetConfigurationBytes()
		{
			return base.GetConfigurationBytes()
				.Concat(BitConverter.GetBytes((UInt16)_FabryPerotConfigurationVersion))
				.Concat(BitConverter.GetBytes(_wavelengthBand))
				.Concat(BitConverter.GetBytes(_calibrationFactor))
				.Concat(BitConverter.GetBytes(_dnFromPeakOverRangeThreshold))
				.Concat(BitConverter.GetBytes(_dnFromPeakThreshold))
				.Concat(BitConverter.GetBytes(_recenterThresholdLow));
		}

		protected override IEnumerable<string> GetConfigurationString()
		{
			return base.GetConfigurationString()
				.Concat(new string[]
				{
					"Wavelength Band (nm): " + _wavelengthBand,
					"Calibration Factor (nm/g): " + _calibrationFactor,
					"Over Range Threshold: " + _dnFromPeakOverRangeThreshold,
					"Peak Threshold: " + _dnFromPeakThreshold,
					"Recenter Threshold Low: " + _recenterThresholdLow
				});
		}

		#endregion
	}
}

