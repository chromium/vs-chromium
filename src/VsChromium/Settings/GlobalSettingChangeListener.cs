// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Reflection;
using VsChromium.Package;

namespace VsChromium.Settings {
  public class GlobalSettingChangeListener<T> : IGlobalSettingChangeListener<T> {
    private readonly IEventBus _eventBus;
    private readonly IGlobalSettingsProvider _globalSettingsProvider;
    private readonly PropertyInfo _propertyInfo;
    private T _currentValue;
    private object _eventBusCookie;

    public GlobalSettingChangeListener(IEventBus eventBus, IGlobalSettingsProvider globalSettingsProvider,  PropertyInfo propertyInfo) {
      _eventBus = eventBus;
      _globalSettingsProvider = globalSettingsProvider;
      _propertyInfo = propertyInfo;
      _eventBusCookie = _eventBus.RegisterHandler("GlobalSettingsChanged", GlobalSettingsChangedHandler);
      _currentValue = (T)propertyInfo.GetValue(_globalSettingsProvider.GlobalSettings);
    }

    public event EventHandler<PropetyChangedEventArgs<T>> PropertyChanged;
    public T Current { get { return _currentValue; } }

    protected virtual void OnPropertyChanged(T oldValue, T newValue) {
      var handler = PropertyChanged;
      if (handler != null) {
        var args = new PropetyChangedEventArgs<T> {
          OldValue = oldValue,
          NewValue = newValue,
        };
        handler(this, args);
      }
    }

    private void GlobalSettingsChangedHandler(object sender, EventArgs eventArgs) {
      var value = _propertyInfo.GetValue(_globalSettingsProvider.GlobalSettings);
      if (Equals(_currentValue, value))
        return;

      var oldValue = _currentValue;
      _currentValue = (T)value;
      OnPropertyChanged(oldValue, _currentValue);
    }

    public void Dispose() {
      if (_eventBusCookie != null) {
        _eventBus.UnregisterHandler(_eventBusCookie);
        _eventBusCookie = null;
      }
    }
  }
}