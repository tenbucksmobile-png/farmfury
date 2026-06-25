import { useState } from 'react';
import { pickImage, uploadImage } from '@/utils/image';
import { useEmployee } from '@/providers/EmployeeContext';

export function useImageUpload(bucket: string) {
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { employee } = useEmployee();

  const upload = async (pathPrefix?: string): Promise<string | null> => {
    try {
      setError(null);
      const uri = await pickImage();
      if (!uri) return null;

      setUploading(true);
      const path = `${pathPrefix || employee?.employee_id}/${Date.now()}`;
      const result = await uploadImage(uri, bucket, path);
      return result.publicUrl;
    } catch (err: any) {
      setError(err.message || 'Upload failed');
      return null;
    } finally {
      setUploading(false);
    }
  };

  return { upload, uploading, error };
}
