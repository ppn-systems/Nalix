// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Nalix.Chat.Client.Avalonia.Views;

public partial class ChatRoomView : UserControl
{
    public ChatRoomView() => this.InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
