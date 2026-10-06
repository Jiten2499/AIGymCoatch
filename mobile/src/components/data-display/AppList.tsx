import { FlatList, type ListRenderItem } from 'react-native';
import { ScreenState } from '../feedback/ScreenState';

type AppListProps<T> = {
  data: readonly T[];
  keyExtractor: (item: T) => string;
  renderItem: ListRenderItem<T>;
  loading?: boolean;
  error?: string | null;
  onRetry?: () => void;
};

export function AppList<T>({ data, keyExtractor, renderItem, loading, error, onRetry }: AppListProps<T>) {
  return (
    <ScreenState empty={data.length === 0} error={error} loading={loading} onRetry={onRetry}>
      <FlatList data={data as T[]} keyExtractor={keyExtractor} renderItem={renderItem} />
    </ScreenState>
  );
}
