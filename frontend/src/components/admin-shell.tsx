'use client';
import styles from './admin-shell.module.css';
import { MotionTabs } from '@/components/motion';
import { createContext, useContext, useState, useEffect } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ClipboardList, Utensils, Settings2, Store } from 'lucide-react';
import { AuthGate, useApp, Loading } from './core';
const AdminContext=createContext({storeId:'',role:''});
export const useStore=()=>useContext(AdminContext);
export default function AdminShell({children}:{children:React.ReactNode}){return <AuthGate><Shell>{children}</Shell></AuthGate>;}
function Shell({children}:{children:React.ReactNode}) {
  const {auth}=useApp();const path=usePathname();const [storeId,setStoreId]=useState('');
  useEffect(()=>{const saved=localStorage.getItem('foodies.adminStore');setStoreId(auth?.memberships.some(m=>m.storeId===saved)?saved!:auth?.memberships[0]?.storeId||'');},[auth]);
  const member=auth?.memberships.find(m=>m.storeId===storeId);
  if(!auth?.memberships.length)return <main className="page"><div className="empty"><Store size={35}/><h1>No tienes tiendas asignadas.</h1><p>El operador de la plataforma puede vincular tu cuenta a una tienda.</p></div></main>;
  if(!member)return <Loading/>;
  return <AdminContext.Provider value={{storeId,role:member.role}}><main className={`page admin-page ${styles.workspace}`}><div className={styles.toolbar}><MotionTabs as="nav" activeKey={path} className="admin-nav" aria-label="Administración">{[['/admin','Pedidos',<ClipboardList key="i" size={17}/>],['/admin/catalogo','Catálogo',<Utensils key="i" size={17}/>],...(member.role==='Manager'?[['/admin/configuracion','Configuración',<Settings2 key="i" size={17}/>]]:[])].map(([href,label,icon])=><Link href={href as string} key={href as string} className={path===href?'selected':''}>{icon}{label}</Link>)}</MotionTabs><label className="store-picker"><Store size={18}/><select aria-label="Tienda activa" value={storeId} onChange={e=>{setStoreId(e.target.value);localStorage.setItem('foodies.adminStore',e.target.value);}}>{auth.memberships.map(m=><option value={m.storeId} key={m.storeId}>{m.name}</option>)}</select><span className="role-label">{member.role==='Manager'?'Encargada/o':'Cocina'}</span></label></div><div key={storeId}>{children}</div></main></AdminContext.Provider>;
}
