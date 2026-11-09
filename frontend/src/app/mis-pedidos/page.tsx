'use client';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, ShoppingBag } from 'lucide-react';
import { api, Order, money, date, statusLabel } from '@/lib/api';
import { AuthGate, ErrorBox, Loading } from '@/components/core';
export default function Orders() { return <AuthGate><OrdersContent/></AuthGate>; }
function OrdersContent() {
  const [orders,setOrders]=useState<Order[]>([]); const [error,setError]=useState(''); const [loading,setLoading]=useState(true);
  async function load(){try{setOrders(await api<Order[]>('/me/orders'));setError('');}catch(e){setError((e as Error).message);}finally{setLoading(false);}}
  useEffect(()=>{void load();const t=setInterval(load,15000);return()=>clearInterval(t);},[]);
  return <main className="page narrow"><span className="eyebrow">DE TU ANTOJO A TU MESA</span><h1>Mis pedidos<span className="orange">.</span></h1><p className="intro">Todo lo que has pedido, en un solo lugar.</p>{error&&<ErrorBox text={error} retry={load}/>} {loading?<Loading/>:!orders.length?<div className="empty"><ShoppingBag size={38}/><h2>Tu primer antojo está por llegar.</h2><p>Explora las tiendas y encuentra algo rico para hoy.</p><Link className="button" href="/">Explorar tiendas <ArrowRight size={17}/></Link></div>:<div className="order-list">{orders.map(o=><Link className="order-list-card" key={o.id} href={`/mis-pedidos/${o.id}`}><div><span className="eyebrow">#{o.publicCode}</span><h3>{o.storeName}</h3><p>{date(o.createdAt)}</p><span className={`status status-${o.status}`}><span className="status-label" key={o.status}>{statusLabel[o.status]}</span></span></div><div><b>{money(o.total)}</b><span>{o.items.reduce((s,i)=>s+i.quantity,0)} artículos</span><ArrowRight size={19}/></div></Link>)}</div>}</main>;
}
