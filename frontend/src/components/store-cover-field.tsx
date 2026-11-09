'use client';
import { useEffect, useRef, useState } from 'react';
import { Upload } from 'lucide-react';
import { api } from '@/lib/api';
import { platformApi } from '@/lib/platform-api';
import { StoreCover } from './store-cover';
import styles from './store-cover-field.module.css';

export function StoreCoverField({ value, onChange, storeId, platform = false, onUploadingChange }: { value?: string | null; onChange: (value: string | null) => void; storeId?: string; platform?: boolean; onUploadingChange?: (uploading: boolean) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const controller = useRef<AbortController | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const managed = value?.startsWith('/api/v1/media/stores/');
  useEffect(() => () => controller.current?.abort(), []);
  async function upload(file?: File) {
    if (!file || !storeId || uploading) return;
    setError('');
    if (file.size > 5 * 1024 * 1024) { setError('La imagen debe pesar como máximo 5 MB.'); return; }
    if (!['image/jpeg','image/png','image/webp'].includes(file.type)) { setError('Selecciona una imagen JPG, PNG o WebP.'); return; }
    setUploading(true); onUploadingChange?.(true);
    controller.current = new AbortController();
    const body = new FormData(); body.append('file',file);
    try {
      const response = await (platform ? platformApi : api)<{url:string}>(`${platform ? '' : '/admin'}/stores/${storeId}/cover-image`, {method:'POST',body,signal:controller.current.signal});
      onChange(response.url);
    } catch (failure) { if (!controller.current.signal.aborted) setError((failure as Error).message); }
    finally { setUploading(false); onUploadingChange?.(false); }
  }
  return <div className={styles.field}>
    <label>Imagen de portada</label>
    <input ref={input} id="store-cover-file" className={styles.fileInput} type="file" accept="image/jpeg,image/png,image/webp" disabled={uploading || !storeId} aria-label="Seleccionar imagen de portada" onChange={event => { const file=event.target.files?.[0];event.target.value='';void upload(file); }}/>
    <button type="button" className="button secondary" disabled={uploading || !storeId} onClick={()=>input.current?.click()}><Upload size={17}/>{uploading?'Subiendo imagen…':managed?'Cambiar imagen':'Subir imagen'}</button>
    <p className={styles.uploadHelp}>{storeId?'JPG, PNG o WebP, hasta 5 MB. Usa una foto horizontal.':'Crea la tienda para poder subir una imagen; mientras tanto puedes usar una URL.'}</p>
    <label htmlFor="store-cover-url">O usa una URL HTTPS
      <input id="store-cover-url" type="url" pattern="https://.*" maxLength={500} disabled={uploading} value={managed ? '' : value || ''} onChange={event => {setError('');onChange(event.target.value || null);}} placeholder="https://…" aria-describedby="store-cover-help"/>
    </label>
    <p id="store-cover-help">Se muestra en la tarjeta de tu tienda. Tamaño recomendado: 1200 × 600 px.</p>
    {error&&<p className={styles.error} role="alert">{error}</p>}
    <div className={styles.preview} aria-busy={uploading}><StoreCover src={value?.trim()} alt="Vista previa de la portada de tu tienda"/></div>
    {managed&&<p role="status" className={styles.uploadHelp}>Imagen subida. Los cambios de portada se publican al guardar la configuración.</p>}
    {value && <button type="button" className="text-button" disabled={uploading} onClick={() => onChange(null)}>Quitar portada</button>}
  </div>;
}
