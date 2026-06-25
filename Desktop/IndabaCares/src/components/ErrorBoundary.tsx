/**
 * ErrorBoundary — PERF-01
 *
 * Catches runtime errors in the React tree and renders a safe fallback.
 * React class component required: only class components can implement
 * componentDidCatch / getDerivedStateFromError.
 *
 * Usage:
 *   <ErrorBoundary>
 *     <SomeScreen />
 *   </ErrorBoundary>
 *
 *   <ErrorBoundary fallback={<Text>Custom error UI</Text>}>
 *     <SomeScreen />
 *   </ErrorBoundary>
 */

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

const PURPLE = '#7B1FA2';

interface Props {
  children: React.ReactNode;
  /** Optional custom fallback. Receives the error and a reset callback. */
  fallback?: (error: Error, reset: () => void) => React.ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends React.Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    // Always log — full stack in dev, sanitised message in production.
    // To add Sentry: replace this line with
    //   Sentry.captureException(error, { extra: { componentStack: info.componentStack } });
    console.error(
      '[ErrorBoundary]',
      error.message,
      __DEV__ ? info.componentStack : '',
    );
  }

  reset = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    const { hasError, error } = this.state;
    const { children, fallback } = this.props;

    if (!hasError) return children;

    if (fallback && error) {
      return <>{fallback(error, this.reset)}</>;
    }

    return (
      <View style={s.container}>
        <ScrollView contentContainerStyle={s.scroll} showsVerticalScrollIndicator={false}>
          <View style={s.iconWrap}>
            <Ionicons name="warning-outline" size={56} color={PURPLE} />
          </View>
          <Text style={s.title}>Something went wrong</Text>
          <Text style={s.subtitle}>
            The app encountered an unexpected error. Your data is safe.
          </Text>

          {__DEV__ && error && (
            <View style={s.debugBox}>
              <Text style={s.debugLabel}>Debug info</Text>
              <Text style={s.debugText} numberOfLines={8} selectable>
                {error.message}
              </Text>
            </View>
          )}

          <TouchableOpacity style={s.btn} onPress={this.reset} activeOpacity={0.8}>
            <Ionicons name="refresh" size={18} color="#fff" />
            <Text style={s.btnTxt}>Try again</Text>
          </TouchableOpacity>
        </ScrollView>
      </View>
    );
  }
}

/**
 * Lightweight screen-level boundary: wraps a single screen so a crash there
 * does not take down the entire navigator.
 */
export function ScreenErrorBoundary({ children }: { children: React.ReactNode }) {
  return <ErrorBoundary>{children}</ErrorBoundary>;
}

const s = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fafafa',
  },
  scroll: {
    flexGrow: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 32,
    paddingVertical: 64,
  },
  iconWrap: {
    width: 96,
    height: 96,
    borderRadius: 48,
    backgroundColor: '#F3E5F5',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 24,
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: '#1e1b4b',
    textAlign: 'center',
    marginBottom: 10,
  },
  subtitle: {
    fontSize: 14,
    color: '#64748b',
    textAlign: 'center',
    lineHeight: 22,
    marginBottom: 32,
  },
  debugBox: {
    width: '100%',
    backgroundColor: '#fff1f2',
    borderRadius: 12,
    padding: 14,
    marginBottom: 24,
    borderWidth: 1,
    borderColor: '#fecdd3',
  },
  debugLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: '#be123c',
    marginBottom: 6,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  debugText: {
    fontSize: 12,
    color: '#be123c',
    fontFamily: 'monospace',
    lineHeight: 18,
  },
  btn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: PURPLE,
    borderRadius: 14,
    paddingVertical: 14,
    paddingHorizontal: 32,
    gap: 8,
    shadowColor: PURPLE,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 6,
  },
  btnTxt: {
    fontSize: 16,
    fontWeight: '700',
    color: '#fff',
  },
});
