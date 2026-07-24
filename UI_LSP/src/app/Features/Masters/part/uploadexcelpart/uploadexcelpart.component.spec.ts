import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UploadexcelpartComponent } from './uploadexcelpart.component';

describe('UploadexcelpartComponent', () => {
  let component: UploadexcelpartComponent;
  let fixture: ComponentFixture<UploadexcelpartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UploadexcelpartComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UploadexcelpartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
