using System;

using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using BeaconTracker;
using BeaconTracker.Models;

using SDKTemplate;

namespace BluetoothAdvertisement
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Scenario5_AggregatingWatcher : Page
    {
        // 
        private readonly Tracker _tracker;
        // Entry point for the background task.
        private readonly string taskEntryPoint = "BackgroundTasks.AdvertisementWatcherTask";
        // A name is given to the task in order for it to be identifiable across context.
        private readonly string taskName = "Scenario5_AggregatingTask";
        // The watcher trigger used to configure the background task registration
        private readonly BluetoothLEAdvertisementWatcherTrigger trigger;
        // A pointer back to the main page is required to display status messages.
        private MainPage rootPage;
        // The background task registration for the background advertisement watcher
        private IBackgroundTaskRegistration taskRegistration;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public Scenario5_AggregatingWatcher()
        {
            InitializeComponent();

            // Create and initialize a new trigger to configure it.
            trigger = new BluetoothLEAdvertisementWatcherTrigger();

            var manufacturerData = new BluetoothLEManufacturerData { CompanyId = 76 };
            trigger.AdvertisementFilter.Advertisement.ManufacturerData.Add(manufacturerData);

            _tracker = new Tracker();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        ///
        /// We will enable/disable parts of the UI if the device doesn't support it.
        /// </summary>
        /// <param name="eventArgs">Event data that describes how this page was reached. The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            rootPage = MainPage.Current;

            // Get the existing task if already registered
            if (taskRegistration == null)
            {
                // Find the task if we previously registered it
                foreach (var task in BackgroundTaskRegistration.AllTasks.Values)
                {
                    if (task.Name == taskName)
                    {
                        taskRegistration = task;
                        taskRegistration.Completed += OnBackgroundTaskCompleted;
                        break;
                    }
                }
            }
            else
                taskRegistration.Completed += OnBackgroundTaskCompleted;

            // Attach handlers for suspension to stop the watcher when the App is suspended.
            App.Current.Suspending += App_Suspending;
            App.Current.Resuming += App_Resuming;

            rootPage.NotifyUser("Press Run to register watcher.", NotifyType.StatusMessage);
        }

        /// <summary>
        /// Invoked immediately before the Page is unloaded and is no longer the current source of a parent Frame.
        /// </summary>
        /// <param name="e">
        /// Event data that can be examined by overriding code. The event data is representative
        /// of the navigation that will unload the current Page unless canceled. The
        /// navigation can potentially be canceled by setting Cancel.
        /// </param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Remove local suspension handlers from the App since this page is no longer active.
            App.Current.Suspending -= App_Suspending;
            App.Current.Resuming -= App_Resuming;

            // Since the watcher is registered in the background, the background task will be triggered when the App is closed 
            // or in the background. To unregister the task, press the Stop button.
            if (taskRegistration != null)
            {
                // Always unregister the handlers to release the resources to prevent leaks.
                taskRegistration.Completed -= OnBackgroundTaskCompleted;
            }
            base.OnNavigatingFrom(e);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            if (taskRegistration != null)
            {
                // Always unregister the handlers to release the resources to prevent leaks.
                taskRegistration.Completed -= OnBackgroundTaskCompleted;
            }
            rootPage.NotifyUser("App suspending.", NotifyType.StatusMessage);
        }

        /// <summary>
        /// Invoked when application execution is being resumed.
        /// </summary>
        /// <param name="sender">The source of the resume request.</param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {
            // Get the existing task if already registered
            if (taskRegistration == null)
            {
                // Find the task if we previously registered it
                foreach (var task in BackgroundTaskRegistration.AllTasks.Values)
                {
                    if (task.Name == taskName)
                    {
                        taskRegistration = task;
                        taskRegistration.Completed += OnBackgroundTaskCompleted;
                        break;
                    }
                }
            }
            else
                taskRegistration.Completed += OnBackgroundTaskCompleted;
        }

        /// <summary>
        /// Invoked as an event handler when the Run button is pressed.
        /// </summary>
        /// <param name="sender">Instance that triggered the event.</param>
        /// <param name="e">Event data describing the conditions that led to the event.</param>
        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            // Registering a background trigger if it is not already registered. It will start background scanning.
            // First get the existing tasks to see if we already registered for it
            if (taskRegistration != null)
                rootPage.NotifyUser("Background watcher already registered.", NotifyType.StatusMessage);
            // Applications registering for background trigger must request for permission.
            BackgroundAccessStatus backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync();
            // Here, we do not fail the registration even if the access is not granted. Instead, we allow 
            // the trigger to be registered and when the access is granted for the Application at a later time,
            // the trigger will automatically start working again.

            // At this point we assume we haven't found any existing tasks matching the one we want to register
            // First, configure the task entry point, trigger and name
            var builder = new BackgroundTaskBuilder();
            builder.TaskEntryPoint = taskEntryPoint;
            builder.SetTrigger(trigger);
            builder.Name = taskName;

            // Now perform the registration. The registration can throw an exception if the current 
            // hardware does not support background advertisement offloading
            try
            {
                taskRegistration = builder.Register();

                // For this scenario, attach an event handler to display the result processed from the background task
                taskRegistration.Completed += OnBackgroundTaskCompleted;

                // Even though the trigger is registered successfully, it might be blocked. Notify the user if that is the case.
                if ((backgroundAccessStatus == BackgroundAccessStatus.Denied) ||
                   (backgroundAccessStatus == BackgroundAccessStatus.Unspecified))
                {
                    rootPage.NotifyUser(
                        "Not able to run in background. Application must given permission to be added to lock screen.",
                        NotifyType.ErrorMessage);
                }
                else
                    rootPage.NotifyUser("Background watcher registered.", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                switch ((uint)ex.HResult)
                {
                case (0x80070032): // ERROR_NOT_SUPPORTED
                    rootPage.NotifyUser("The hardware does not support background advertisement offload.",
                        NotifyType.ErrorMessage);
                    break;
                default:
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Invoked as an event handler when the Stop button is pressed.
        /// </summary>
        /// <param name="sender">Instance that triggered the event.</param>
        /// <param name="e">Event data describing the conditions that led to the event.</param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Unregistering the background task will stop scanning if this is the only client requesting scan
            // First get the existing tasks to see if we already registered for it
            if (taskRegistration != null)
            {
                taskRegistration.Unregister(true);
                taskRegistration = null;
                rootPage.NotifyUser("Background watcher unregistered.", NotifyType.StatusMessage);
            }
            else
            {
                // At this point we assume we haven't found any existing tasks matching the one we want to unregister
                rootPage.NotifyUser("No registered background watcher found.", NotifyType.StatusMessage);
            }
        }

        /// <summary>
        /// Handle background task completion.
        /// </summary>
        /// <param name="task">The task that is reporting completion.</param>
        /// <param name="e">Arguments of the completion report.</param>
        private async void OnBackgroundTaskCompleted(BackgroundTaskRegistration task,
                                                     BackgroundTaskCompletedEventArgs eventArgs)
        {
            foreach (var value in ApplicationData.Current.LocalSettings.Values)
            {
                await
                    Dispatcher.RunAsync(CoreDispatcherPriority.Low,
                        () => ReceivedAdvertisementListBox.Items.Add("Completed with key: " + value.Key));
            }

            // We get the advertisement(s) processed by the background task
            if (ApplicationData.Current.LocalSettings.Values.Keys.Contains(taskName))
            {
                var reading = (BeaconReading)ApplicationData.Current.LocalSettings.Values[taskName];
                // Serialize UI update to the main UI thread
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        //_tracker.AddBeaconReading(reading);
                        // Display these information on the list
                        ReceivedAdvertisementListBox.Items.Add(reading.EventMessage);
                    });
            }
        }
    }
}