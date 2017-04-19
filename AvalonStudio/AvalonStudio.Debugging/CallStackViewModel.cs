using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Plugin;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace AvalonStudio.Debugging
{
    public class CallStackViewModel : ToolViewModel, IExtension
    {
        private IDebugManager2 _debugManager;

        private FrameViewModel selectedFrame;

        public CallStackViewModel()
        {
            Title = "CallStack";

            Dispatcher.UIThread.InvokeAsync(() => { IsVisible = false; });

            Frames = new ObservableCollection<FrameViewModel>();
        }

        public FrameViewModel SelectedFrame
        {
            get
            {
                return selectedFrame;
            }
            set
            {
                selectedFrame = value;

                if (selectedFrame != null)
                {
                    var shell = IoC.Get<IShell>();

                    //shell?.OpenDocument(shell?.CurrentSolution?.FindFile(selectedFrame.Model.FullFileName), selectedFrame.Model.Line, -1, -1, true, true);
                }

                this.RaisePropertyChanged(nameof(SelectedFrame));
            }
        }

        public ObservableCollection<FrameViewModel> Frames { get; set; }

        public override Location DefaultLocation
        {
            get { return Location.BottomRight; }
        }

        public void BeforeActivation()
        {
        }

        public void Activation()
        {
            _debugManager = IoC.Get<IDebugManager2>();

            _debugManager.DebugSessionStarted += (sender, e) => { IsVisible = true; };

            _debugManager.DebugSessionEnded += (sender, e) =>
            {
                IsVisible = false;
                Clear();
            };
        }

        public void Clear()
        {
            //Frames.Clear();
        }

        /*public void Update(List<Frame> frames)
		{
			if (frames != null)
			{
				Frames.Clear();

				foreach (var frame in frames)
				{
					Frames.Add(new FrameViewModel(_debugManager, frame));
				}
			}
		}*/
    }
}