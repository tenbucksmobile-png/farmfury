import React, { forwardRef, useState } from 'react';
import {
  View,
  TextInput as RNTextInput,
  Text,
  Pressable,
  type TextInputProps as RNTextInputProps,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

interface TextInputProps extends RNTextInputProps {
  label?: string;
  error?: string;
  hint?: string;
}

export const TextInput = forwardRef<RNTextInput, TextInputProps>(
  ({ label, error, hint, className, secureTextEntry, ...props }, ref) => {
    const [hidden, setHidden] = useState(true);
    const isPassword = secureTextEntry === true;

    return (
      <View className="mb-4">
        {label && (
          <Text className="mb-1.5 text-sm font-medium text-slate-700">{label}</Text>
        )}
        <View className="relative">
          <RNTextInput
            ref={ref}
            secureTextEntry={isPassword ? hidden : false}
            className={`rounded-2xl border bg-white py-3.5 text-base text-slate-900 ${
              isPassword ? 'pl-4 pr-12' : 'px-4'
            } ${error ? 'border-danger-500' : 'border-slate-200 focus:border-primary-400'} ${
              className || ''
            }`}
            placeholderTextColor="#94a3b8"
            {...props}
          />
          {isPassword && (
            <Pressable
              onPress={() => setHidden((h) => !h)}
              hitSlop={8}
              className="absolute bottom-0 right-0 top-0 w-12 items-center justify-center"
            >
              <Ionicons
                name={hidden ? 'eye-outline' : 'eye-off-outline'}
                size={20}
                color="#94a3b8"
              />
            </Pressable>
          )}
        </View>
        {error && (
          <Text className="mt-1 text-xs text-danger-500">{error}</Text>
        )}
        {hint && !error && (
          <Text className="mt-1 text-xs text-slate-400">{hint}</Text>
        )}
      </View>
    );
  }
);

TextInput.displayName = 'TextInput';
