import React from 'react';
import { Pressable, Text, ActivityIndicator, type PressableProps } from 'react-native';

interface ButtonProps extends PressableProps {
  title: string;
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  loading?: boolean;
  icon?: React.ReactNode;
}

const VARIANTS = {
  primary:   'bg-primary-500 active:bg-primary-600',
  secondary: 'bg-slate-100 active:bg-slate-200',
  outline:   'border border-primary-500 bg-transparent active:bg-primary-50',
  ghost:     'bg-transparent active:bg-primary-50',
  danger:    'bg-danger-500 active:bg-danger-600',
} as const;

const TEXT_VARIANTS = {
  primary:   'text-white',
  secondary: 'text-slate-700',
  outline:   'text-primary-500',
  ghost:     'text-primary-600',
  danger:    'text-white',
} as const;

const SIZES = {
  sm: 'px-3 py-2 rounded-lg',
  md: 'px-5 py-3.5 rounded-2xl',
  lg: 'px-6 py-4 rounded-2xl',
} as const;

const TEXT_SIZES = {
  sm: 'text-sm',
  md: 'text-base',
  lg: 'text-lg',
} as const;

export function Button({
  title,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled,
  icon,
  ...props
}: ButtonProps) {
  const isDisabled = disabled || loading;

  return (
    <Pressable
      className={`flex-row items-center justify-center ${VARIANTS[variant]} ${SIZES[size]} ${isDisabled ? 'opacity-50' : ''}`}
      disabled={isDisabled}
      {...props}
    >
      {loading ? (
        <ActivityIndicator
          size="small"
          color={variant === 'primary' || variant === 'danger' ? '#fff' : '#7c3aed'}
          className="mr-2"
        />
      ) : icon ? (
        <>{icon}</>
      ) : null}
      <Text className={`font-semibold ${TEXT_VARIANTS[variant]} ${TEXT_SIZES[size]}`}>
        {title}
      </Text>
    </Pressable>
  );
}
