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
