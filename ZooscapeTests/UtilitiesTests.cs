using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Zooscape.Domain.Utilities;

namespace ZooscapeTests;

public class UtilitiesTests(ITestOutputHelper testOutputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    /// <summary>
    /// This test generates a million values and asserts that the resultant set falls within the
    /// specified minimum and maximum values and that the average and standard deviation values
    /// fall within 1% of the specified values. It also draws a histogram of the resultant set.
    /// </summary>
    [Theory]
    [InlineData(100, 10, 50, 150)] // Generic example
    [InlineData(50, 7.5, 25, 75)] // Example of default powerup spawn randomisation
    [InlineData(250, 5, 225, 275)] // Example of default zookeeper spawn randomisation
    public void GlobalSeededRngTest(double mean, double stdDev, double min, double max)
    {
        // Arrange
        var rng = new GlobalSeededRandomizer(1);
        var values = new List<double>();

        // Act
        for (int i = 0; i < 1000000; i++)
            values.Add(rng.NormalNextDouble(mean, stdDev, min, max));
        var calcMean = values.Average();
        var calcStdDev = Math.Sqrt(values.Average(v => Math.Pow(v - calcMean, 2)));
        _testOutputHelper.WriteLine($"min = {values.Min()}");
        _testOutputHelper.WriteLine($"max = {values.Max()}");
        _testOutputHelper.WriteLine($"avg = {calcMean}");
        _testOutputHelper.WriteLine($"stdDev = {calcStdDev}");
        TestUtils.DrawHistogram(
            testOutputHelper: _testOutputHelper,
            values: values,
            min: min,
            max: max,
            rows: 10,
            cols: 100
        );

        // Assert
        Assert.InRange(values.Min(), min, max);
        Assert.InRange(values.Max(), min, max);
        Assert.True(FloatingPoint.RoughlyEqual(calcMean, mean, 0.01 * mean));
        Assert.True(FloatingPoint.RoughlyEqual(calcStdDev, stdDev, 0.01 * stdDev));
    }

    /// <summary>
    /// This tests checks that the GlobalSeededRandomizer always produces the same output
    /// given the same seed.
    /// </summary>
    /// <param name="seed"></param>
    [Theory]
    [InlineData(100, 100, 10, 50, 150)]
    [InlineData(1235, 50, 7.5, 25, 75)]
    [InlineData(9999999, 250, 5, 225, 275)]
    public void GlobalSeededRngConsistencyTest(
        int seed,
        double mean,
        double stdDev,
        double min,
        double max
    )
    {
        List<List<double>> values = [];
        for (int i = 0; i < 10; i++)
        {
            var rng = new GlobalSeededRandomizer(seed);
            List<double> innerValues = [];
            for (int j = 0; j < 1000; j++)
            {
                innerValues.Add(rng.NormalNextDouble(mean, stdDev, min, max));
            }
            values.Add(innerValues);
        }
        values
            .Skip(1)
            .ToList()
            .ForEach(innerValues =>
            {
                Assert.Equal(innerValues, values[0]);
            });
    }

    /// <summary>
    /// This test checks that the GlobalSeededRandomizer quits correctly after exceeding the timeout
    /// to get a random value given various sources
    /// </summary>
    [Theory]
    [InlineData(100, 5)] // small world
    [InlineData(625, 6)] // small - medium world
    [InlineData(2601, 10)] // large world
    public void GlobalSeededRngEfficiencyTest(int size, int timeout)
    {
        // Arrange
        var stopwatch = new Stopwatch();
        var rng = new GlobalSeededRandomizer(1);
        var values = Enumerable.Range(0, size).Select(_ => new Random().Next(0, 100)).ToArray();

        // Act
        stopwatch.Start();
        var randomValue = rng.GetRandomElement(values, _ => false, timeout);
        stopwatch.Stop();

        //Assert
        Assert.True(randomValue == 0);

        //Allow up to 10ms overhead
        var elapsed = stopwatch.ElapsedMilliseconds;
        Assert.InRange(elapsed, timeout, timeout + 10);
    }
}
