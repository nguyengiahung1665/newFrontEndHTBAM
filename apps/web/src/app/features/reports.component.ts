import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { PageTitleComponent, errorText } from '../shared/ui';

@Component({standalone:true,imports:[FormsModule,PageTitleComponent],changeDetection:ChangeDetectionStrategy.OnPush,template:`
  <app-page-title title="Báo cáo" subtitle="Phân tích hành vi, chuyên cần và chất lượng quan sát từ dữ liệu thật.">
    @if(data()){<button type="button" class="secondary" (click)="export('excel')">Xuất Excel</button><button type="button" (click)="export('pdf')">Xuất PDF</button>}
  </app-page-title>
  <div class="filter-bar"><label>Loại báo cáo<select [(ngModel)]="kind" (ngModelChange)="data.set(null)"><option value="sessions">Buổi học</option><option value="students">Sinh viên</option><option value="class-sections">Lớp học phần</option></select></label><label class="grow">ID đối tượng<input type="number" [(ngModel)]="id" placeholder="Nhập ID" /></label><button type="button" [disabled]="!id||loading()" (click)="load()">{{loading()?'Đang tải…':'Xem báo cáo'}}</button></div>
  @if(error()){<div class="error-box">{{error()}}</div>}
  @if(data()){
    <div class="stats stats-four">
      @for(metric of summaryEntries();track metric.key){<section class="stat-card"><span>{{metric.label}}</span><strong>{{metric.value}}</strong><small>Dữ liệu tổng hợp</small></section>}
    </div>
    <div class="dashboard-grid">
      <section class="card"><div class="card-header" style="margin:-20px -20px 18px"><div><h3>Phân tích hành vi</h3><p>Tỷ lệ theo phạm vi báo cáo</p></div></div>
        @for(metric of behaviorEntries();track metric.label){<div class="metric-row"><span>{{metric.label}}</span><div class="progress-track"><div class="progress-value" [style.width.%]="metric.value" [style.background]="metric.color"></div></div><strong>{{metric.value}}%</strong></div>}
        @empty{<div class="empty-state" style="min-height:150px"><div><strong>Chưa có tỷ lệ hành vi</strong><span>API không trả về dữ liệu tổng hợp cho phạm vi này.</span></div></div>}
      </section>
      <section class="card"><div class="card-header" style="margin:-20px -20px 18px"><div><h3>Nhận xét tự động</h3><p>AI Analytics</p></div><span class="badge purple">AI</span></div>
        @if(autoComment()){<p style="line-height:1.7;color:#475569">{{autoComment()}}</p>}@else{<div class="empty-state" style="min-height:150px"><div><strong>Chưa có nhận xét</strong><span>Nhận xét AI chưa được tạo cho báo cáo này.</span></div></div>}
      </section>
    </div>
    <section class="card table-card"><div class="card-header"><div><h3>Dữ liệu chi tiết</h3><p>{{rows().length}} bản ghi</p></div></div><div class="table-wrap"><table><thead><tr>@for(column of columns();track column){<th>{{columnLabel(column)}}</th>}</tr></thead><tbody>
      @for(row of rows();track $index){<tr>@for(column of columns();track column){<td>{{formatValue(row[column])}}</td>}</tr>}@empty{<tr><td [attr.colspan]="columns().length||1" class="empty">Không có dữ liệu chi tiết.</td></tr>}
    </tbody></table></div></section>
  }@else{<section class="card"><div class="empty-state"><div><strong>Chọn phạm vi để xem báo cáo</strong><span>Nhập ID buổi học, sinh viên hoặc lớp học phần.</span></div></div></section>}
`})
export class ReportsComponent{
  private readonly api=inject(ApiService);kind:'sessions'|'students'|'class-sections'='sessions';id:number|null=null;readonly data=signal<any>(null);readonly error=signal('');readonly loading=signal(false);
  load(){if(!this.id)return;this.loading.set(true);this.error.set('');this.api.report(this.kind,this.id).subscribe({next:(value)=>{this.data.set(value);this.loading.set(false)},error:(error)=>{this.error.set(errorText(error));this.loading.set(false)}})}
  summaryEntries(){const source=this.data()?.summary??{};return Object.entries(source).slice(0,4).map(([key,value])=>({key,label:this.columnLabel(key),value:this.formatValue(value)}))}
  behaviorEntries(){const source=this.data()?.summary??{};const map=[['focusedRatio','Tập trung','#16a34a'],['averageFocused','Tập trung','#16a34a'],['focused','Tập trung','#16a34a'],['distractedRatio','Mất tập trung','#f59e0b'],['averageDistracted','Mất tập trung','#f59e0b'],['distracted','Mất tập trung','#f59e0b'],['sleepyRatio','Buồn ngủ','#dc2626'],['averageSleepy','Buồn ngủ','#dc2626'],['sleepy','Buồn ngủ','#dc2626'],['activeRatio','Hoạt động','#2563eb'],['averageActive','Hoạt động','#2563eb'],['active','Hoạt động','#2563eb']] as const;const seen=new Set<string>();return map.filter(([key,label])=>source[key]!=null&&!seen.has(label)&&!!seen.add(label)).map(([key,label,color])=>({label,value:Math.round(Number(source[key])*100),color}))}
  rows():any[]{const value=this.data();return value?.students??value?.sessions??value?.timeline??value?.attendance??value?.attention??[]}
  columns():string[]{const row=this.rows()[0];return row?Object.keys(row).filter(key=>!key.toLowerCase().includes('comment')&&!key.toLowerCase().includes('provider')).slice(0,8):[]}
  autoComment():string{const value=this.data();return value?.summary?.autoComment??value?.classSection?.autoComment??value?.session?.autoComment??value?.students?.find((item:any)=>item.autoComment)?.autoComment??''}
  columnLabel(key:string):string{const labels:Record<string,string>={sessionCount:'Số buổi',averageFocused:'Tập trung TB',averageDistracted:'Mất tập trung TB',averageSleepy:'Buồn ngủ TB',averageActive:'Hoạt động TB',averageAttendanceScore:'Điểm chuyên cần',totalObservedSeconds:'Thời gian quan sát',totalAlerts:'Cảnh báo',focused:'Tập trung',distracted:'Mất tập trung',sleepy:'Buồn ngủ',active:'Hoạt động',alerts:'Cảnh báo',studentCode:'MSSV',fullName:'Họ tên',scheduledStart:'Thời gian'};return labels[key]??key.replace(/([A-Z])/g,' $1').trim()}
  formatValue(value:any):string{if(value==null)return'—';if(typeof value==='number'&&value>=0&&value<=1)return`${Math.round(value*100)}%`;if(typeof value==='boolean')return value?'Có':'Không';if(typeof value==='object')return'Xem chi tiết';return String(value)}
  export(format:'pdf'|'excel'){if(!this.id)return;this.api.exportReport(this.kind,this.id,format).subscribe({next:(blob)=>{const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download=`HTBAM-${this.kind}-${this.id}.${format==='excel'?'xlsx':'pdf'}`;anchor.click();URL.revokeObjectURL(url)},error:(error)=>this.error.set(errorText(error))})}
}
