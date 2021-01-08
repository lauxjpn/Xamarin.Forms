﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.MobileBlazorBindings.WebView.Elements;
using Microsoft.MobileBlazorBindings.WebView.Windows;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;
using Xamarin.Forms.Platform.UWP;
using XF = Xamarin.Forms;

[assembly: ExportRenderer(typeof(WebViewExtended), typeof(WebViewExtendedAnaheimRenderer))]

namespace Microsoft.MobileBlazorBindings.WebView.Windows
{
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
	public class WebViewExtendedAnaheimRenderer : ViewRenderer<WebViewExtended, WebView2>, XF.IWebViewDelegate
#pragma warning restore CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
	{
        private CoreWebView2Environment _coreWebView2Environment;
        //private ulong _navigationId;
        private Uri _currentUri;
        private bool _isDisposed;

        protected override void OnElementChanged(ElementChangedEventArgs<WebViewExtended> e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            _ = HandleElementChangedAsync(e);
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            base.OnElementPropertyChanged(sender, e);

            switch (e.PropertyName)
            {
                case "Source":
                    Load();
                    break;
            }
        }

        private const string LoadBlazorJSScript =
            "window.onload = (function blazorInitScript() {" +
            "    var blazorScript = document.createElement('script');" +
            "    blazorScript.src = 'framework://blazor.desktop.js';" +
            "    document.head.appendChild(blazorScript);" +
            "});";

        private async Task HandleElementChangedAsync(ElementChangedEventArgs<WebViewExtended> e)
        {
            if (e.OldElement != null)
            {
                Element.SendMessageFromJSToDotNetRequested -= OnSendMessageFromJSToDotNetRequested;
            }

            if (e.NewElement != null)
            {
                if (Control == null)
                {
                    try
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope; this object's lifetime is managed elsewhere
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
						var nativeControl = new WebView2() { MinHeight = 200 };
#pragma warning restore CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning restore CA2000 // Dispose objects before losing scope
						SetNativeControl(nativeControl);

                        _coreWebView2Environment = await CoreWebView2Environment.CreateAsync(/*userDataFolder: BlazorHybridWindows.WebViewDirectory*/);

                        await nativeControl.EnsureCoreWebView2Async();

                        await Control.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.external = { sendMessage: function(message) { window.chrome.webview.postMessage(message); }, receiveMessage: function(callback) { window.chrome.webview.addEventListener(\'message\', function(e) { callback(e.data); }); } };");
                        await Control.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(LoadBlazorJSScript);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        e.NewElement.ErrorHandler?.HandleException(ex);
                        return;
                    }
                    SubscribeToControlEvents();
                }

                Load();

                SubscribeToElementEvents();

                // There is a weird bug in WebView2 where on 200% DPI it does not redraw the WebView2 until you
                // send a WM_WINDOWPOSCHANGING message to the child window that serves as a host for WebView2.
                // this sends the required message.
                //Control.UpdateWindowPos();
            }

            base.OnElementChanged(e);

        }

        private void SubscribeToElementEvents()
        {
            Element.SendMessageFromJSToDotNetRequested += OnSendMessageFromJSToDotNetRequested;
        }

        private void SubscribeToControlEvents()
        {
            Control.NavigationStarting += HandleNavigationStarting;
            Control.NavigationCompleted += HandleNavigationCompleted;
            Control.WebMessageReceived += HandleWebMessageReceived;
            Control.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            Control.CoreWebView2.WebResourceRequested += HandleWebResourceRequested;
        }

#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        private void HandleNavigationCompleted(object sender, WebView2NavigationCompletedEventArgs e)
#pragma warning restore CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        {
            //if (e.NavigationId == _navigationId)
            {
                Element.HandleNavigationFinished(_currentUri);
            }
        }

#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        private void HandleNavigationStarting(object sender, WebView2NavigationStartingEventArgs e)
#pragma warning restore CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        {
            //_navigationId = e.NavigationId;
            _currentUri = new Uri(e.Uri);

            Element.HandleNavigationStarting(_currentUri);
        }

        private void OnSendMessageFromJSToDotNetRequested(object sender, string message)
        {
            Control.CoreWebView2.PostWebMessageAsString(message);
        }

#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        private void HandleWebMessageReceived(object sender, WebView2WebMessageReceivedEventArgs args)
#pragma warning restore CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        {
            Element.HandleWebMessageReceived(args.WebMessageAsString);
        }

        private void HandleWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var uriString = args.Request.Uri;
            var uri = new Uri(uriString);
            if (Element.SchemeHandlers.TryGetValue(uri.Scheme, out var handler) && _coreWebView2Environment != null)
            {
                var responseStream = handler(uriString, out var responseContentType);
                if (responseStream != null) // If null, the handler doesn't want to handle it
                {
                    responseStream.Position = 0;

					args.Response = _coreWebView2Environment.CreateWebResourceResponse(
						Content: responseStream.AsRandomAccessStream(),
						StatusCode: 200,
						ReasonPhrase: "OK",
						Headers: $"Content-Type: {responseContentType}{Environment.NewLine}Cache-Control: no-cache, max-age=0, must-revalidate, no-store");
				}
			}
        }

        private void Load()
        {
            try
            {
                if (Element.Source != null)
                {
                    Element.Source.Load(this);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Element?.ErrorHandler?.HandleException(ex);
            }
        }

#pragma warning disable CA1054 // Uri parameters should not be strings
        public void LoadHtml(string html, string baseUrl)
#pragma warning restore CA1054 // Uri parameters should not be strings
        {
            if (html != null)
            {
                Control.NavigateToString(html);
            }
        }

#pragma warning disable CA1054 // Uri parameters should not be strings
        public void LoadUrl(string url)
#pragma warning restore CA1054 // Uri parameters should not be strings
        {
            if (url != null && Control?.CoreWebView2 != null)
            {
                Control.CoreWebView2.Navigate(url);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                if (Control != null)
                {
                    Control.CoreWebView2.WebResourceRequested -= HandleWebResourceRequested;
                    Control.CoreWebView2.RemoveWebResourceRequestedFilter("*", Web.WebView2.Core.CoreWebView2WebResourceContext.All);
                    Control.WebMessageReceived -= HandleWebMessageReceived;
                    Control.NavigationStarting -= HandleNavigationStarting;
                    Control.NavigationCompleted -= HandleNavigationCompleted;
                    switch (Control.Parent)
                    {
                        //case FormsPanel formsPanel:
                        //    formsPanel.Children.Remove(Control);
                        //    break;
                        case ContentControl contentControl:
                            contentControl.Content = null;
                            break;
                        // might be null when the control is not properly initialized. Don't crash on a NullReference then.
                        case null:
                            break;
                        default:
                            throw new NotImplementedException($"Don't know how to detach from a parent of type {Control.Parent.GetType().FullName}");
                    }
                }
            }

            _isDisposed = true;
            base.Dispose(disposing);
        }
    }
}
