using EventBusCore;
using UnityEngine;

namespace EGS.SocketIO.ExampleSample
{
    public sealed class StartExerciseEvent : IEvent
    {
        public readonly string ExerciseId;
        public StartExerciseEvent(string exerciseId) => ExerciseId = exerciseId;
    }
    public sealed class WeatherChangedEvent : IEvent
    {
        public readonly bool IsRaining;
        public WeatherChangedEvent(bool rain) => IsRaining = rain;
    }
}