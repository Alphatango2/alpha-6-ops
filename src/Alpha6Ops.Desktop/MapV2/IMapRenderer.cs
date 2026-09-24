using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop.MapV2;

internal interface IMapRenderer : IAsyncDisposable
{
    FrameworkElement View { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
    void Offer(MapSnapshot snapshot);
    void Present(MapPresentationState presentation);
    event Action<MapSelection?>? SelectionChanged;
    event Action<MapCameraState>? CameraChanged;
    event Action<MapRendererStatus>? StatusChanged;
}
