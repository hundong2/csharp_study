# Listview

## gridview

- Gridview column DisplayMemberBinding

```
<GridView>
    <GridView.Columns>
        <GridViewColumn Header="No" Width="100" DisplayMemberBinding="{Binding Value, UpdateSourceTrigger=PropertyChanged,Mode=TwoWay}">
        <GridViewColumn Width="200" Header="Key">
            <GridViewColumn.CellTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding Value2}"></TextBlock>
                    </StackPanel>
                </DataTemplate>
            </GridViewColumn.CellTemplate>
        </GridViewColumn>
    </GridView.Columns>
</GridView>
```

## Binding

### Width, Height Binding

- 특정 xaml의 element의 width/Height와 동일하게 연동하고 싶은 경우 사용.

```
<Grid x:Name="ElementName" />

Width="{Binding Path=ActualWidth, ElementName=ElementName }"
```
